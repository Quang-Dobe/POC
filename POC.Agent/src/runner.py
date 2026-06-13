import json
import uuid
from collections.abc import Iterator

import openai

from .config import Config
from .dab_client import query_dab
from .agent import QUERY_DAB_TOOL_DEF, AGENT_INSTRUCTIONS

_MAX_TOOL_ROUNDS = 10


def _initial_messages(question: str) -> list[dict]:
    return [
        {"role": "system", "content": AGENT_INSTRUCTIONS},
        {"role": "user", "content": question},
    ]


def _append_tool_round(
    messages: list[dict],
    msg,
    config: Config,
    token: str,
    api_role: str,
) -> None:
    messages.append({
        "role": "assistant",
        "content": msg.content,
        "tool_calls": [
            {
                "id": tc.id,
                "type": "function",
                "function": {"name": tc.function.name, "arguments": tc.function.arguments},
            }
            for tc in msg.tool_calls
        ],
    })

    for tc in msg.tool_calls:
        args = json.loads(tc.function.arguments)
        result = query_dab(config.dab_base_url, token, api_role, **args)
        messages.append({"role": "tool", "tool_call_id": tc.id, "content": result})


def run_agent(
    openai_client: openai.OpenAI,
    question: str,
    session_id: str | None,
    token: str,
    api_role: str,
    config: Config,
) -> tuple[str, str]:
    messages = _initial_messages(question)

    for _ in range(_MAX_TOOL_ROUNDS):
        response = openai_client.chat.completions.create(
            model=config.model_deployment_name,
            messages=messages,
            tools=[QUERY_DAB_TOOL_DEF],
        )

        choice = response.choices[0]
        msg = choice.message

        if choice.finish_reason in ("stop", "end_turn") or not msg.tool_calls:
            return msg.content or "", session_id or str(uuid.uuid4())

        _append_tool_round(messages, msg, config, token, api_role)

    raise RuntimeError("Agent tool loop did not converge within max rounds")


class _ToolCallAccumulator:
    def __init__(self) -> None:
        self.id: str | None = None
        self._name: str = ""
        self._arguments: str = ""

    def absorb(self, delta_tool_call) -> None:
        if delta_tool_call.id:
            self.id = delta_tool_call.id
        fn = delta_tool_call.function
        if fn is not None:
            if fn.name:
                self._name += fn.name
            if fn.arguments:
                self._arguments += fn.arguments

    @property
    def function(self) -> "_ToolCallAccumulator":
        return self

    @property
    def name(self) -> str:
        return self._name

    @property
    def arguments(self) -> str:
        return self._arguments


class _StreamedAssistantMessage:
    def __init__(self, content: str | None, tool_calls: list[_ToolCallAccumulator]) -> None:
        self.content = content
        self.tool_calls = tool_calls


def run_agent_stream(
    openai_client: openai.OpenAI,
    question: str,
    session_id: str | None,
    token: str,
    api_role: str,
    config: Config,
) -> Iterator[str]:
    messages = _initial_messages(question)

    for _ in range(_MAX_TOOL_ROUNDS):
        stream = openai_client.chat.completions.create(
            model=config.model_deployment_name,
            messages=messages,
            tools=[QUERY_DAB_TOOL_DEF],
            stream=True,
        )

        content_parts: list[str] = []
        tool_calls_by_index: dict[int, _ToolCallAccumulator] = {}
        finish_reason: str | None = None

        for event in stream:
            if not event.choices:
                continue
            choice = event.choices[0]
            delta = choice.delta

            if delta is not None and delta.content:
                content_parts.append(delta.content)
                yield delta.content

            if delta is not None and delta.tool_calls:
                for delta_tc in delta.tool_calls:
                    acc = tool_calls_by_index.setdefault(delta_tc.index, _ToolCallAccumulator())
                    acc.absorb(delta_tc)

            if choice.finish_reason is not None:
                finish_reason = choice.finish_reason

        tool_calls = [tool_calls_by_index[i] for i in sorted(tool_calls_by_index)]
        msg = _StreamedAssistantMessage(
            content="".join(content_parts) or None,
            tool_calls=tool_calls,
        )

        if finish_reason in ("stop", "end_turn") or not tool_calls:
            return

        _append_tool_round(messages, msg, config, token, api_role)

    raise RuntimeError("Agent tool loop did not converge within max rounds")
