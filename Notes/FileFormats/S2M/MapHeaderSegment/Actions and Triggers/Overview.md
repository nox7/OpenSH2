Read [Triggers](./Triggers/Trigger.md) and [Actions](./Actions/Action.md).

- [[Object]] bytes
- 4 bytes
- 4 bytes
- 4 bytes
- 4 bytes
- 4 bytes
- 1 byte (00)
- 4 bytes (Always A5C1FFFF) = Denotes the beginning of the action/trigger configuration data
- 4 bytes object registration index
- Variable bytes (this is the specific action or trigger configuration data)
- Terminating AF1EFFFF object end marker