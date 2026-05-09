Quick overview (diagram to come, once I figure out a few bits):

Initiating action in the Hub
    1. An EventProvider/Agent submits an Event to the Hub
    2. This Event gets logged onto an EventStack and a key/ref is returned to the EventProvider (for future notifications/updates applied to that Event)

The hub has a pool of Event threads watching for new events being added to its stack
    1. Pop event off stack
    2. Do a quick evaluation (type, originating user) and 
    3. pass to a collection of Config rule providers (push onto their stacks)

The hub has a collection of config rule providers, each of which have their own pool of threads for talking to the external rule providers [run some stored procedures on a DB/call a webservice/custom .dll]
    1. Pop event of config event request stack
    2. Call rule provider .evaluate method
    3. Get back a collection of actions that need to occur (includes what Agent will perform the action & what to tell that Agent)
    4. Notify Hub that each Action has been requested to be done for this EventKey
    5. Pass each action (include key to event) to its respective Agent
We will have a default configuration provider which just does a pass through using the filter meta data.
The hub has a collection of Agents, each of which have their own pool of threads for doing the actual work/this could be a good spot for using hangire.io?
True, at the moment hang fire gets used for anything we don't need the web HTTP context for
    • Sending email
    • Generating reports,
    • Processing datasets 

    Depending on the agent, it might be a sequential call (in which case the standard thread model works nicely) or it could be an asynchronous call/wait for response (a different thread model is required, as we would need paired threads? Send and monitor for responses)
    
    Either way any feedback/response should be fed back to the Hub using the EventKey & ActionKey

RV. Ok so is the state of the request building as you go through each sequential agent or is it static, or is this a mode of operation?

I think by default everything is async, if a web request triggering an event needs a response (like a search) then we can pass the associated io.stream around in a wrapper class that implements a fifo queue, async results go into the queue and the writer processes one returning result into the io.stream at a time.

I think we can say that by default it's a rest API between the internal and external world this it’s a stream of bytes in the body for responses, order of results is not guaranteed.


As message goes through the hub each stage should add a timestamp of when it entered and got processed. This can then be used by the controller to:
    • Maintain list of inflight messages
    • Report on latency/performance issues

