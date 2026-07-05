# UAT Application Insights Queries

Use these queries in Application Insights > Logs after the UAT App Service has `APPLICATIONINSIGHTS_CONNECTION_STRING` configured.

Some workspace-based Application Insights resources expose table names like `AppRequests` instead of `requests`. If a query says the table does not exist, select the matching table from the Logs schema and keep the same filters.

## Live Error Board

```kusto
requests
| where timestamp > ago(30m)
| summarize
    Total=count(),
    Failed=countif(success == false),
    P95DurationMs=percentile(duration, 95)
  by bin(timestamp, 1m), name
| order by timestamp desc
```

## Failed Requests

```kusto
requests
| where timestamp > ago(24h)
| where success == false or toint(resultCode) >= 500
| project timestamp, operation_Id, name, url, resultCode, duration, cloud_RoleName
| order by timestamp desc
```

## Exceptions

```kusto
exceptions
| where timestamp > ago(24h)
| project timestamp, operation_Id, type, outerMessage, method, cloud_RoleName
| order by timestamp desc
```

## Trace By Operation Id

Replace the value with the `operation_Id` from a failed request or exception.

```kusto
let operationId = "<operation-id>";
union requests, exceptions, traces, dependencies
| where operation_Id == operationId
| project timestamp, itemType, operation_Id, name, message, resultCode, success, duration, severityLevel
| order by timestamp asc
```

## Trace By X-Correlation-Id

Ask testers to capture the `X-Correlation-Id` response header from a failed request. The backend also uses this value as `HttpContext.TraceIdentifier` and log scope `CorrelationId`.

```kusto
let correlationId = "<x-correlation-id>";
union requests, exceptions, traces, dependencies
| extend CustomDimensions = todynamic(customDimensions)
| extend CorrelationId = tostring(CustomDimensions.CorrelationId)
| extend LogMessage = tostring(column_ifexists("message", ""))
| where operation_Id == correlationId
   or CorrelationId == correlationId
   or LogMessage has correlationId
| project timestamp, itemType, operation_Id, CorrelationId, name, LogMessage, resultCode, success, duration, severityLevel
| order by timestamp asc
```

## Slow Requests

```kusto
requests
| where timestamp > ago(24h)
| where duration > 3000ms
| project timestamp, operation_Id, name, url, resultCode, duration
| order by duration desc
```

## Dependency Failures

```kusto
dependencies
| where timestamp > ago(24h)
| where success == false
| summarize Count=count(), LastSeen=max(timestamp) by type, target, name, resultCode
| order by Count desc
```

## Background Job Failures

```kusto
traces
| where timestamp > ago(24h)
| where severityLevel >= 3
| where message has_any ("worker failed", "background", "notification", "billing", "top up", "mail")
| project timestamp, operation_Id, severityLevel, message, cloud_RoleName
| order by timestamp desc
```

## SignalR And Notification Clues

```kusto
union requests, traces
| where timestamp > ago(24h)
| where url has "/hubs/notifications"
   or name has "notifications"
   or message has_any ("SignalR", "NotificationHub", "notification")
| project timestamp, itemType, operation_Id, name, url, resultCode, message
| order by timestamp desc
```

## UAT Workbook Tiles

Create an Application Insights Workbook with these tiles:

- Requests/min and failed requests/min: use `Live Error Board`.
- Top exceptions: use `Exceptions`, grouped by `type` and `outerMessage`.
- Slowest endpoints: use `Slow Requests`, grouped by `name`.
- Dependency failures: use `Dependency Failures`.
- Background job issues: use `Background Job Failures`.

Set the workbook time range to 30 minutes for active UAT sessions and 24 hours for daily review.
