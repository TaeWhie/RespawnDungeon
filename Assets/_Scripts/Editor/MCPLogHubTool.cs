using MCPForUnity.Editor.Helpers;
using MCPForUnity.Editor.Tools;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// MCPLogHub configuration tool exposed via Unity MCP custom tools.
/// Execute with: execute_custom_tool(tool_name="mcp_log_hub", parameters={...})
/// </summary>
[McpForUnityTool("mcp_log_hub")]
public static class MCPLogHubTool
{
    public static object HandleCommand(JObject @params)
    {
        if (@params == null)
            return new ErrorResponse("Parameters cannot be null.");

        var p = new ToolParams(@params);
        string action = p.Get("action");
        if (string.IsNullOrEmpty(action))
            return new ErrorResponse("'action' is required.");

        switch (action)
        {
            case "set_issue_log_enabled":
            {
                string issueId = p.Get("issue_id");
                if (string.IsNullOrWhiteSpace(issueId))
                    return new ErrorResponse("'issue_id' is required.");

                bool enabled = p.GetBool("enabled", true);
                MCPLogHub.SetIssueLogEnabled(issueId, enabled);
                return new SuccessResponse("Issue log toggle updated.", new
                {
                    action,
                    issue_id = issueId,
                    enabled
                });
            }

            case "get_issue_log_enabled":
            {
                string issueId = p.Get("issue_id");
                if (string.IsNullOrWhiteSpace(issueId))
                    return new ErrorResponse("'issue_id' is required.");

                bool enabled = MCPLogHub.IsIssueLogEnabled(issueId);
                return new SuccessResponse("Issue log toggle fetched.", new
                {
                    action,
                    issue_id = issueId,
                    enabled
                });
            }

            case "set_trace_channel":
            {
                string channel = p.Get("channel");
                if (string.IsNullOrWhiteSpace(channel))
                    return new ErrorResponse("'channel' is required.");

                bool enabled = p.GetBool("enabled", true);
                float interval = p.GetFloat("interval") ?? 0.2f;
                MCPLogHub.ConfigureTraceChannel(channel, enabled, Mathf.Max(0.05f, interval));
                return new SuccessResponse("Trace channel updated.", new
                {
                    action,
                    channel,
                    enabled,
                    interval = Mathf.Max(0.05f, interval)
                });
            }

            case "get_trace_channel":
            {
                string channel = p.Get("channel");
                if (string.IsNullOrWhiteSpace(channel))
                    return new ErrorResponse("'channel' is required.");

                bool enabled = MCPLogHub.IsTraceChannelEnabled(channel);
                float interval = MCPLogHub.GetTraceChannelInterval(channel, 0.2f);
                return new SuccessResponse("Trace channel fetched.", new
                {
                    action,
                    channel,
                    enabled,
                    interval
                });
            }

            case "apply_preset":
            {
                string preset = p.Get("preset");
                if (string.IsNullOrWhiteSpace(preset))
                    return new ErrorResponse("'preset' is required.");

                switch (preset)
                {
                    case "navigation_debug":
                        MCPLogHub.SetIssueLogEnabled("NAV_START", true);
                        MCPLogHub.ConfigureTraceChannel("EXPLORER_NAV", true, 0.15f);
                        MCPLogHub.ConfigureTraceChannel("FOLLOWER_NAV", true, 0.2f);
                        break;

                    case "leader_focus":
                        MCPLogHub.SetIssueLogEnabled("NAV_START", true);
                        MCPLogHub.ConfigureTraceChannel("EXPLORER_NAV", true, 0.1f);
                        MCPLogHub.ConfigureTraceChannel("FOLLOWER_NAV", false, 0.2f);
                        break;

                    case "quiet":
                        MCPLogHub.SetIssueLogEnabled("NAV_START", false);
                        MCPLogHub.ConfigureTraceChannel("EXPLORER_NAV", false, 0.2f);
                        MCPLogHub.ConfigureTraceChannel("FOLLOWER_NAV", false, 0.2f);
                        break;

                    default:
                        return new ErrorResponse(
                            "Unknown preset. Supported presets: navigation_debug, leader_focus, quiet");
                }

                return new SuccessResponse("Preset applied.", new
                {
                    action,
                    preset,
                    nav_start = MCPLogHub.IsIssueLogEnabled("NAV_START"),
                    explorer_nav = new
                    {
                        enabled = MCPLogHub.IsTraceChannelEnabled("EXPLORER_NAV"),
                        interval = MCPLogHub.GetTraceChannelInterval("EXPLORER_NAV", 0.2f)
                    },
                    follower_nav = new
                    {
                        enabled = MCPLogHub.IsTraceChannelEnabled("FOLLOWER_NAV"),
                        interval = MCPLogHub.GetTraceChannelInterval("FOLLOWER_NAV", 0.2f)
                    }
                });
            }

            default:
                return new ErrorResponse(
                    "Unknown action. Supported actions: " +
                    "set_issue_log_enabled, get_issue_log_enabled, set_trace_channel, get_trace_channel, apply_preset");
        }
    }
}
