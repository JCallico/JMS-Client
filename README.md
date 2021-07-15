# JMS Client

A simple client console app for WebLogic JMS.

It uses the [WebLogic JMS .NET client](https://docs.oracle.com/cd/E24329_01/web.1211/e24386/toc.htm) to interact with WebLogic JMS.

Currently only sending and receiving from topics is supported. 

## Sample usages

### Sending a message

```
JMSClient.exe -c "Send" -m "This is a sample message"
```

### Receiving messages

```
JMSClient.exe -c "Receive"
```
