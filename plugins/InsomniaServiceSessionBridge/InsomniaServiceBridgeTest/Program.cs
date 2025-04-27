// See https://aka.ms/new-console-template for more information
using MadWizard.Insomnia.Minion.Messages;
using MadWizard.Insomnia.Minion.Pipe;

Console.WriteLine("Hello, World!");

var server = new MessagePipeServer(1);

server.Connected += Server_Connected;
server.Disconnected += Server_Disconnected;

void Server_Disconnected(object? sender, EventArgs e)
{
    Console.WriteLine("disconnected");
}

void Server_Connected(object? sender, EventArgs e)
{
    Console.WriteLine("connected");
}

server.MessageReceived += MessageReceived;

void MessageReceived(object? sender, Message message)
{
    if (message is InputTimeMessage input)
        Console.WriteLine($"Received: {message.GetType().Name} -> {input.LastInputTime}");
    else
        Console.WriteLine($"Received: {message.GetType().Name}");
}

server.Start();

server.SendMessage(new TerminateMessage());

Console.ReadKey();

