# And Then A Miracle Occurs (Atamo)

## Overview (what is it?)

The idea behind this project is to create an auditable, pluggable, self-hostable service to support asynchronous processing of messages, they can be:

- unidirectional: client sends message into Atamo. Agents then receive those events to do things with.
- bidirectional: client sends a request message into Atamo and receives a requestid back. As agents process and respond to the request, their responses are made available to be picked up by the client.

Messages are routed to agents based off rules applied by configuration providers.

A single message can be routed to multiple agents for actioning.

All paths that a message takes (which client posted it, configuration rules that evaluated it for routing, agent assigned message, etc..) will be communicated to auditor and optionally back to client.

Every element of Atamo should be extendable with reasonable intial default implementations.

Atamo can be used:

- inside a project to support asynchronous multi threaded processing 
- as a restful api service to support rule driven event processing
- or even as a user driven agentic interface

## dump of notes



Next set of sub pages would become the user documentation for how to interact with Atamo.Hub (the user being a programmer hooking into the API.



Sections required:

	• Overview (describe in general terms what it does)

		○ Primary use cases

			§ Log event and rules will work out what [n] actions will fire because of that event (user/team/organisational level of rules)

				□ Sub case 1: Fire and forget (this is the primary one - Controller gets info about state/failures - client does not need to maintain connection to Hub)

				□ Sub case 2: Fire and monitor (as well as the Controller receiving telemetry on where the EventMessage is and details about the Actions fired, the Client can also receive/retrieve these details) 

			§ Request a response/search on generic RequestType - retrieves details from multiple sources and feeds it back to requestor(s) - includes caching and disconnected requestor

				□ Sub case 1: load/search semi-static data - once retrieved client is disconnected from Hub

				□ Sub case 2: load/search and receive updates - initial load given to client, connection remains open as client receives updates. (either client disconnects or all Agents involved in feed terminate feed)

				□ Sub case 3: client sends request in and then comes back later to check on result

					® Should this be part of the event provider? It could be a cache layer wrapped around the core hub as an additional service, if there is a shared request guid to check back on, they hold the results in blob storage external to the primary system

					® Maintain a keep alive time setting before clearing the data from the hub? 

		○ How it can be used:

			§ Stateless - Inside a winforms app to aid in making it multi-threaded/more responsive

			§ Stateful web/service - to guarantee that actions will be dealt with

		○ Components:

			§ Hub : central engine (all settings are applied via the Hub)

				□ Itelemetry - interface to notify Controller and Client what's happening inside the hub

				□ IResponse - how to feedback to client it's specific responses to request

				□ IHubControl - interface controller talk to hub through

				□ IHubReceiver - interface client will submit requests through

			§ Controller: single controller that hosts the hub

				□ this controls registering of agents, event providers and configuration providers

				□ Receives telemetry from the hub for audit, performance and state recording purposes

				□ Manages if this is a stateless or stateful hub

				□ Monitoring of hub state can be done via the hub

					® Alert on latency

					® Alert on performance issues

					® Monitor throughout

					® ...

			§ EventProviders: provides a natural interface to the clients

				□ registers new event types on the hub

				□ packages the event messages for submission to the hub

				□ Manages any responses to the client, including support for disconnected clients

			§ EventTypes : collection of event types that the Hub can receive

				□ Event types register with an event message template (used to build the action msg)

				□ The hub uses event type & originating user to filter which config rules fire

			§ ActionTypes : collection of possible actions that can be done (by multiple Agents)

			§ Agents : the components that do the work of talking to an external point, impersonating a particular user to do an 'action' (ie: send email)

				□ Can be generic agents where agent meta data provides destination details ie: call restful webpage (meta = base Web address, action = folders/params)

				□ Call stored proc (meta = Sql conn string and sp name, action = params)

				□ So register of an agent includes dll location, init meta data, action template

			§ Configuration providers: default is only one basic provider that links event type & user to an agent (allows simple substitution in populating action template from the event msg - creates the action msg)

				□ New configuration providers can be registered for certain event types/actions, allows more complex rules of when to fire actions and how to parse event msg to populate the action msg

				□ When .evaluate called, returns a collection of action messages (default provider only returns 0 or 1 action message)

			§ Configuration rule: link a request/event from a user to n actions

				□ default provider only to allow 1 action per config rule, but multiple rules can match a single request/event

				□ Rule holds meta data that provider will use with event msg to determine:

					® Agents to fire

					® Additional filtering

					® Body of the action msg to send to the agent

			§ Users

				□ Each user has a list of tokens that can be used for each agent provider

				□ Need to work out how to secure safely

				□ Also need some form of group management so we can set a config rule for a group of users. The paying tenant perhaps?

		○ Guides: include set of instructions on building/using the hub

			i. Add hub to small win forms app

				□ Implements the controller

			ii. Make load of data asynch with form responsive

				□ Create an agent and configure it to be responsible for a request

				□ Show telemetry received by the controller

			iii. Load data from multiple sources

				□ Create a second agent and configure to fire with the same request

			iv. Setup a feed of data

				□ Modify 2nd agent to not complete request, instead send a continual feed of data

				□ Also change client so that it disconnects when no longer wants the results

			v. Log an event

				□ Log an event type, at this stage it will do nothing

			vi. Setup action to fire when event occurs

				□ Add new action type to existing agent and configure to fire when event detected

			vii. 2nd action to fire but fire with different user and different action message

			viii. Failures:

				□ Force a failure on one of the agents and show retry attempts

				□ Show alerting when retries exceed attempts

				□ Show how client can be configured to receive alerts from its events (or left to controller to handle)

			ix. Create your own complex configuration rule provider (to do something more complex than just pass through)

			

	• Not sure if it was covered but we need a user can submit their own mapping DLL.

		○ AB: a user can submit their own config rule via the hub using any of the loaded config providers, they can also submit their own config provider if more custom logic required

			§ Will need to have auditing and strict permission handling on who can submit what

			§ I think we should also build in a way to mock/test/verify new rules/components at runtime

	• planning to have 2 levels of cache, 1 at the event provider allowing for disconnected clients. A second in the hub agent manager, where if we get multiple requests for the same data, we only need one call out to the agent/earlier feedback to client

