# Application Insights Cho UAT

Dùng tài liệu này sau khi App Service đã có:

```text
APPLICATIONINSIGHTS_CONNECTION_STRING
```

## Query Failed Requests

```kusto
requests
| where timestamp > ago(24h)
| where success == false
| project timestamp, name, url, resultCode, duration, operation_Id
| order by timestamp desc
```

## Query Exceptions

```kusto
exceptions
| where timestamp > ago(24h)
| project timestamp, type, outerMessage, operation_Id, problemId
| order by timestamp desc
```

## Tìm Theo Operation Id

```kusto
let operationId = "<operation-id>";
union requests, dependencies, exceptions, traces
| where operation_Id == operationId
| order by timestamp asc
```

## Tìm Theo X-Correlation-Id

```kusto
let correlationId = "<x-correlation-id>";
traces
| where timestamp > ago(24h)
| where message contains correlationId
   or tostring(customDimensions.CorrelationId) == correlationId
| order by timestamp desc
```

## Slow Requests

```kusto
requests
| where timestamp > ago(24h)
| where duration > 1000ms
| project timestamp, name, url, duration, resultCode, operation_Id
| order by duration desc
```

## Dependency Failures

```kusto
dependencies
| where timestamp > ago(24h)
| where success == false
| project timestamp, target, name, type, resultCode, duration, operation_Id
| order by timestamp desc
```

## Background Job Logs

```kusto
traces
| where timestamp > ago(24h)
| where message has_any ("Worker", "Background", "TopUp", "CourseBilling", "MailDelivery")
| project timestamp, severityLevel, message, operation_Id, customDimensions
| order by timestamp desc
```

## SignalR / Notification Clues

```kusto
union requests, traces, exceptions
| where timestamp > ago(24h)
| where name has_any ("notifications", "hubs")
   or message has_any ("SignalR", "NotificationHub", "notification")
   or outerMessage has_any ("SignalR", "NotificationHub", "notification")
| project timestamp, itemType, name, message, outerMessage, resultCode, operation_Id
| order by timestamp desc
```

## Gợi Ý Dashboard Workbook

Tạo Application Insights Workbook với các tile:

- Failed requests 24h.
- Exceptions 24h.
- Slow requests.
- Dependency failures.
- Background job traces.
- SignalR/notification clues.
- Search theo `X-Correlation-Id`.
