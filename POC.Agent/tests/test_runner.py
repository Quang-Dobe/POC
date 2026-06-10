import json
import pytest
from unittest.mock import MagicMock, patch


def _make_choice(finish_reason, content=None, tool_calls=None):
    choice = MagicMock()
    choice.finish_reason = finish_reason
    choice.message.content = content
    choice.message.tool_calls = tool_calls or []
    return choice


def _make_tool_call(tc_id, name, args_dict):
    tc = MagicMock()
    tc.id = tc_id
    tc.function.name = name
    tc.function.arguments = json.dumps(args_dict)
    return tc


def test_run_agent_returns_answer_on_stop():
    from src.runner import run_agent

    client = MagicMock()
    client.chat.completions.create.return_value.choices = [
        _make_choice("stop", content="The answer is 42.")
    ]

    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"

    answer, session_id = run_agent(client, "what is 42?", None, "tok", "reader", config)

    assert answer == "The answer is 42."
    assert session_id  # generated uuid


def test_run_agent_executes_tool_call_and_submits_output():
    from src.runner import run_agent

    client = MagicMock()
    tc = _make_tool_call("tc-1", "query_dab", {"entity": "Geography"})
    tool_call_choice = _make_choice("tool_calls", tool_calls=[tc])
    final_choice = _make_choice("stop", content="Manhattan County.")

    client.chat.completions.create.side_effect = [
        MagicMock(choices=[tool_call_choice]),
        MagicMock(choices=[final_choice]),
    ]

    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"

    with patch("src.runner.query_dab", return_value='{"value":[]}') as mock_dab:
        answer, _ = run_agent(client, "where?", None, "tok", "manager", config)

    mock_dab.assert_called_once_with(
        "http://dab:8000", "tok", "manager", entity="Geography"
    )
    assert answer == "Manhattan County."
    assert client.chat.completions.create.call_count == 2


def test_run_agent_preserves_session_id():
    from src.runner import run_agent

    client = MagicMock()
    client.chat.completions.create.return_value.choices = [
        _make_choice("stop", content="ok")
    ]

    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"

    _, session_id = run_agent(client, "hi", "existing-session", "tok", "reader", config)

    assert session_id == "existing-session"


def test_run_agent_raises_when_loop_exceeds_max_rounds():
    from src.runner import run_agent, _MAX_TOOL_ROUNDS

    client = MagicMock()
    tc = _make_tool_call("tc-1", "query_dab", {"entity": "Trip"})
    tool_call_choice = _make_choice("tool_calls", tool_calls=[tc])
    client.chat.completions.create.return_value = MagicMock(choices=[tool_call_choice])

    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"

    with patch("src.runner.query_dab", return_value="[]"):
        with pytest.raises(RuntimeError, match="converge"):
            run_agent(client, "q", None, "tok", "reader", config)

    assert client.chat.completions.create.call_count == _MAX_TOOL_ROUNDS
