# JMS Client

An experimental project to understand different JMS clients.

[![Build Solution](https://github.com/JCallico/JMS-Client/actions/workflows/build_solution.yml/badge.svg)](https://github.com/JCallico/JMS-Client/actions/workflows/build_solution.yml)
[![CodeQL](https://github.com/JCallico/JMS-Client/actions/workflows/codeql-analysis.yml/badge.svg)](https://github.com/JCallico/JMS-Client/actions/workflows/codeql-analysis.yml)
[![DevSkim](https://github.com/JCallico/JMS-Client/actions/workflows/devskim-analysis.yml/badge.svg)](https://github.com/JCallico/JMS-Client/actions/workflows/devskim-analysis.yml)

## TIBCO EMS Client

A simple client console app for TIBCO EMS.

* It uses the [TIBCO EMS .NET API](https://docs.tibco.com/pub/ems/8.6.0/doc/html/api/dotnetdoc/html/namespace_t_i_b_c_o_1_1_e_m_s.html) to interact with TIBCO EMS.

* Currently only sending and receiving from topics is supported.

* Targets .NET Core 3.1

[![Build TIBCO EMS Client](https://github.com/JCallico/JMS-Client/actions/workflows/build_tibco_ems_client.yml/badge.svg)](https://github.com/JCallico/JMS-Client/actions/workflows/build_tibco_ems_client.yml)

### Sample usages

#### Sending a message

```
TibcoEmsClient.exe -c "Send" -m "This is a sample message"
```

#### Receiving messages

```
TibcoEmsClient.exe -c "Receive"
```

## WebLogic JMS Client

A simple client console app for WebLogic JMS.

* It uses the [WebLogic JMS .NET client](https://docs.oracle.com/cd/E24329_01/web.1211/e24386/toc.htm) to interact with WebLogic JMS.

* Currently only sending and receiving from topics is supported.

* Targets .NET Framework 4.8
	* The WebLogic JMS .NET client targets .NET Framework 2.0

### Sample usages

#### Sending a message

```
WebLogicJMSClient.exe -c "Send" -m "This is a sample message"
```

#### Receiving messages

```
WebLogicJMSClient.exe -c "Receive"
```
