using System;
using System.Collections.Concurrent;
using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace RabbitMQRpcClient
{
    public class RpcClient: IDisposable
    {
        private string HostName { get; set; } = "localhost";
        private int Port { get; set; } = 5672;
        private string Username { get; set; } = "guest";
        private string Password { get; set; } = "guest";
        private string RequestQueueName { get; set; } = "q.acme.command";
        private string ReplyQueueName { get; set; } = "amq.rabbitmq.reply-to"; // internal directly reply-to queue
        private string ExchangeName { get; set; } = "x.acme";
        private string RoutingKey { get; set; } = "rpc"; // internal directly reply-to queue
        private bool Durable { get; set; } = false;

        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly EventingBasicConsumer _consumer;
        private readonly BlockingCollection<string> _respQueue = new BlockingCollection<string>();
        private readonly IBasicProperties _props;

        public RpcClient()
        {
            var factory = new ConnectionFactory { HostName = HostName, Port = Port, UserName = Username,  Password = Password };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            //channel.ExchangeDeclare(ExchangeName, ExchangeType.Direct, false, false, null);

            _channel.QueueDeclare(queue: RequestQueueName, durable: Durable, exclusive: false, autoDelete: false, arguments: null);
            //channel.QueueBind(RequestQueueName, ExchangeName, RoutingKey, null);
            //channel.BasicQos(0, 1, false);

            _consumer = new EventingBasicConsumer(_channel);

            _props = _channel.CreateBasicProperties();
            _props.ReplyTo = ReplyQueueName;

            _channel.BasicConsume(
                consumer: _consumer,
                queue: ReplyQueueName,
                autoAck: true);
        }

        public string Call(string message)
        {

            var messageBytes = Encoding.UTF8.GetBytes(message);
            var correlationId = Guid.NewGuid().ToString();
            _props.CorrelationId = correlationId;

            void ConsumerHandler(object model, BasicDeliverEventArgs ea)
            {
                var body = ea.Body;
                var response = Encoding.Default.GetString(body);
                if (ea.BasicProperties.CorrelationId == correlationId)
                {
                    _respQueue.Add(response);
                }
            }

            _consumer.Received -= ConsumerHandler;
            _consumer.Received += ConsumerHandler;

            _channel.BasicPublish(
                exchange: ExchangeName,
                routingKey: RoutingKey,
                basicProperties: _props,
                body: messageBytes);

            return _respQueue.Take();
        }
        
        public void Dispose()
        {
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}