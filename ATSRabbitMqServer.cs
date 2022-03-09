using System;
using System.Collections.Generic;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using ATS.DarkSearch.Extensions;
using RabbitMqWorker = ATS.DarkSearch.Workers.RabbitMqWorker;

namespace ATS.DarkSearch;

public class ATSRabbitMqServer : RabbitMqServer
{
    private readonly Dictionary<Type, string> handlerExchangeMap
        = new Dictionary<Type, string>();

    public ATSRabbitMqServer(string connectionString="localhost",
        string username = null, string password = null)
        : base(connectionString, username, password)
    {
    }

    public void RegisterHandler<T>(Func<IMessage<T>, object> processMessageFn, int noOfThreads, string exchange)
    {
        RegisterHandler(processMessageFn, null, noOfThreads);
        if (exchange != null) handlerExchangeMap[typeof(T)] = exchange;
    }
    
    public override void Init()
    {
        base.Init();

        // process also registration for different exchanges
        using (var connection = ConnectionFactory.CreateConnection())
        using (var channel = connection.CreateModel())
        {
            foreach (var entry in handlerExchangeMap)
            {
                var msgType = entry.Key;
                var exchange = entry.Value;

                // exchange
                if (exchange == RabbitMqWorker.DelayedMessagesExchange)
                {
                    channel.RegisterDelayedMessagesExchange();
                }
                else
                {
                    channel.RegisterDirectExchange(exchange);
                }

                // first delete already created In queue
                var queueNames = new QueueNames(msgType);
                channel.DeleteQueues(new []
                {
                    queueNames.In,
                    queueNames.Priority // also this queue, we don't want to use it
                });
                
                // create a new queue for a specific exchange
                channel.RegisterQueue(queueNames.In, exchange);
            }
        }        
    }
}
