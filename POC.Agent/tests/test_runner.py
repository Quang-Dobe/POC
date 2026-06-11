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


def _content_event(text):
    delta = MagicMock()
    delta.content = text
    delta.tool_calls = None
    choice = MagicMock()
    choice.delta = delta
    choice.finish_reason = None
    return MagicMock(choices=[choice])


def _finish_event(reason="stop"):
    delta = MagicMock()
    delta.content = None
    delta.tool_calls = None
    choice = MagicMock()
    choice.delta = delta
    choice.finish_reason = reason
    return MagicMock(choices=[choice])


def _tool_call_delta(index, *, tc_id=None, name=None, args_fragment=None):
    delta_tc = MagicMock()
    delta_tc.index = index
    delta_tc.id = tc_id
    fn = MagicMock()
    fn.name = name
    fn.arguments = args_fragment
    delta_tc.function = fn
    return delta_tc


def _tool_call_event(delta_tool_calls, finish_reason=None):
    delta = MagicMock()
    delta.content = None
    delta.tool_calls = delta_tool_calls
    choice = MagicMock()
    choice.delta = delta
    choice.finish_reason = finish_reason
    return MagicMock(choices=[choice])


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
    assert session_id


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


def test_run_agent_forwards_passed_in_dab_token_to_query_dab():
    from src.runner import run_agent

    client = MagicMock()
    tc = _make_tool_call("tc-1", "query_dab", {"entity": "Geography"})
    client.chat.completions.create.side_effect = [
        MagicMock(choices=[_make_choice("tool_calls", tool_calls=[tc])]),
        MagicMock(choices=[_make_choice("stop", content="done")]),
    ]

    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"

    with patch("src.runner.query_dab", return_value="[]") as mock_dab:
        run_agent(client, "q", None, "internal-token", "reader", config)

    assert mock_dab.call_args[0][1] == "internal-token"


def test_runner_has_no_token_exchange_dependency():
    import src.runner as runner
    assert not hasattr(runner, "exchange_token")
    assert "token_exchange" not in dir(runner)


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


def _streaming_config():
    config = MagicMock()
    config.model_deployment_name = "gpt-4o"
    config.dab_base_url = "http://dab:8000"
    return config


def test_run_agent_stream_yields_multiple_chunks_in_order():
    from src.runner import run_agent_stream

    client = MagicMock()
    client.chat.completions.create.return_value = iter([
        _content_event("Hello"),
        _content_event(", "),
        _content_event("world"),
        _finish_event("stop"),
    ])

    chunks = list(run_agent_stream(client, "hi", None, "tok", "reader", _streaming_config()))

    assert chunks == ["Hello", ", ", "world"]
    assert client.chat.completions.create.call_args.kwargs["stream"] is True


def test_run_agent_stream_skips_empty_content_deltas():
    from src.runner import run_agent_stream

    client = MagicMock()
    client.chat.completions.create.return_value = iter([
        _content_event(""),
        _content_event("A"),
        _content_event(None),
        _content_event("B"),
        _finish_event("stop"),
    ])

    chunks = list(run_agent_stream(client, "hi", None, "tok", "reader", _streaming_config()))

    assert chunks == ["A", "B"]


def test_run_agent_stream_reassembles_tool_call_deltas_and_runs_dab():
    from src.runner import run_agent_stream

    client = MagicMock()
    round1 = iter([
        _tool_call_event([_tool_call_delta(0, tc_id="tc-1", name="query_dab", args_fragment='{"ent')]),
        _tool_call_event([_tool_call_delta(0, args_fragment='ity": "Geography"}')]),
        _finish_event("tool_calls"),
    ])
    round2 = iter([
        _content_event("Manhattan"),
        _content_event(" County."),
        _finish_event("stop"),
    ])
    client.chat.completions.create.side_effect = [round1, round2]

    with patch("src.runner.query_dab", return_value='{"value":[]}') as mock_dab:
        chunks = list(run_agent_stream(client, "where?", None, "tok", "manager", _streaming_config()))

    mock_dab.assert_called_once_with(
        "http://dab:8000", "tok", "manager", entity="Geography"
    )
    assert chunks == ["Manhattan", " County."]
    assert client.chat.completions.create.call_count == 2


def test_run_agent_stream_raises_when_loop_exceeds_max_rounds():
    from src.runner import run_agent_stream, _MAX_TOOL_ROUNDS

    def _tool_round(*_args, **_kwargs):
        return iter([
            _tool_call_event([_tool_call_delta(0, tc_id="tc-1", name="query_dab", args_fragment='{"entity":"Trip"}')]),
            _finish_event("tool_calls"),
        ])

    client = MagicMock()
    client.chat.completions.create.side_effect = _tool_round

    with patch("src.runner.query_dab", return_value="[]"):
        with pytest.raises(RuntimeError, match="converge"):
            list(run_agent_stream(client, "q", None, "tok", "reader", _streaming_config()))

    assert client.chat.completions.create.call_count == _MAX_TOOL_ROUNDS


def test_run_agent_stream_is_lazy_no_model_call_before_iteration():
    from src.runner import run_agent_stream

    client = MagicMock()
    gen = run_agent_stream(client, "hi", None, "tok", "reader", _streaming_config())

    client.chat.completions.create.assert_not_called()
    client.chat.completions.create.return_value = iter([_content_event("x"), _finish_event("stop")])
    assert list(gen) == ["x"]
