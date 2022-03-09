using System.Collections.Generic;
using ATS.DarkSearch.Helpers;
using RabbitMQ.Client;
using ServiceStack;
using ServiceStack.Messaging;
using ServiceStack.RabbitMq;
using RabbitMqWorker = ATS.DarkSearch.Workers.RabbitMqWorker;

namespace ATS.DarkSearch.Extensions;

public static class RabbitMqExtensions
{
    public static void PublishDelayed<T>(this RabbitMqQueueClient mqClient, IMessage<T> message)
    {
        mqClient.Publish(message.ToInQueueName(), message, RabbitMqWorker.DelayedMessagesExchange);
    }
    
    public static void RegisterDelayedMessagesExchange(this IModel channel)
    {
        channel.ExchangeDeclare(Workers.RabbitMqWorker.DelayedMessagesExchange, "x-delayed-message", 
            true, false, new Dictionary<string, object>()
            {
                { "x-delayed-type", "direct" }
            });
    }

    public static void RegisterQueue(this IModel channel, string queueName, string exchange)
    {
        var args = new Dictionary<string, object> {
            {"x-dead-letter-exchange", QueueNames.ExchangeDlq },
            {"x-dead-letter-routing-key", queueName.Replace(".inq",".dlq").Replace(".priorityq",".dlq") },
        };

        AppHostHelpers.GetMessageService<ATSRabbitMqServer>()?
            .CreateQueueFilter?.Invoke(queueName, args);

        if (!QueueNames.IsTempQueue(queueName)) //Already declared in GetTempQueueName()
        {
            channel.QueueDeclare(queueName, durable: true, exclusive: false, autoDelete: false, arguments: args);
        }
            
        channel.QueueBind(queueName, exchange, routingKey: queueName);
    }
}