import json
import uuid
import openai
from .config import Config
from .dab_client import query_dab
from .agent import QUERY_DAB_TOOL_DEF, AGENT_INSTRUCTIONS

_MAX_TOOL_ROUNDS = 10


def run_agent(
    openai_client: openai.OpenAI,
    question: str,
    session_id: str | None,
    dab_token: str,
    api_role: str,
    config: Config,
) -> tuple[str, str]:
    messages = [
        {"role": "system", "content": AGENT_INSTRUCTIONS},
        {"role": "user", "content": question},
    ]

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

        # Append assistant message with tool calls
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

        # Execute each tool call and append results
        for tc in msg.tool_calls:
            args = json.loads(tc.function.arguments)
            result = query_dab(config.dab_base_url, dab_token, api_role, **args)
            messages.append({"role": "tool", "tool_call_id": tc.id, "content": result})

    raise RuntimeError("Agent tool loop did not converge within max rounds")
