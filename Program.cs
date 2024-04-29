using Telegram.Bot;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Npgsql;
using Microsoft.Extensions.Logging;

namespace BusinessBot
{
    internal class Program
    {
        static async Task Main(string[] args)
        {
            string connectionString = $"";
            NpgsqlConnection connection = new(connectionString);
            connection.Open();
            using var cmd = new NpgsqlCommand();
            cmd.Connection = connection;
            

            var botClient = new TelegramBotClient("");

            using CancellationTokenSource cts = new();

            // StartReceiving does not block the caller thread. Receiving is done on the ThreadPool.
            ReceiverOptions receiverOptions = new()
            {
                AllowedUpdates = Array.Empty<UpdateType>() // receive all update types except ChatMember related updates
            };

            botClient.StartReceiving(
                updateHandler: HandleUpdateAsync,
                pollingErrorHandler: HandlePollingErrorAsync,
                receiverOptions: receiverOptions,
                cancellationToken: cts.Token
            );

            var me = await botClient.GetMeAsync();

            Console.WriteLine($"Start listening for @{me.Username}");
            Console.ReadLine();

            // Send cancellation request to stop bot
            cts.Cancel();

            async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
            {
                try
                {
                    if (update.BusinessConnection != null)
                    {
                        if (update.BusinessConnection.IsEnabled)
                        {
                            Console.WriteLine($"Connection established! {update.BusinessConnection.Id.ToString()}");
                            Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: update.BusinessConnection.UserChatId,
                                text: "Connection established!",
                                cancellationToken: cancellationToken);
                            cmd.CommandText = $"INSERT INTO connections(id, user_chat_id) SELECT '{update.BusinessConnection.Id.ToString()}', '{update.BusinessConnection.UserChatId.ToString()}' WHERE NOT EXISTS (SELECT id FROM connections WHERE id = '{update.BusinessConnection.Id.ToString()}')";
                            await cmd.ExecuteNonQueryAsync();
                        }
                        else
                        {
                            Console.WriteLine($"Connection removed! {update.BusinessConnection.Id.ToString()}");
                            Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: update.BusinessConnection.UserChatId,
                                text: "Connection removed!",
                                cancellationToken: cancellationToken);
                            cmd.CommandText = $"DELETE from connections WHERE id = '{update.BusinessConnection.Id.ToString()}'";
                            await cmd.ExecuteNonQueryAsync();
                        }

                    }

                    if (update.BusinessMessage != null)
                    {
                        Console.WriteLine("New message");
                        if ((update.BusinessMessage.From.Id == null) || (update.BusinessMessage.Text == null))
                        {
                            return;
                        }
                        cmd.CommandText = $"INSERT INTO messages(message_id, from_id, text) VALUES('{update.BusinessMessage.MessageId}', '{update.BusinessMessage.From.Id}', '{update.BusinessMessage.Text}')";
                        await cmd.ExecuteNonQueryAsync();

                    }


                    if (update.DeletedBusinessMessages != null)
                    {

                        await using var command0 = new NpgsqlCommand($"SELECT user_chat_id from connections where id = '{update.DeletedBusinessMessages.BusinessConnectionId}'", connection);
                        await using var reader0 = await command0.ExecuteReaderAsync();
                        await reader0.ReadAsync();
                        string id = reader0.GetString(0);
                        await reader0.CloseAsync();
                        Console.WriteLine("New deleted message!");

                        foreach (int message_id in update.DeletedBusinessMessages.MessageIds)
                        {
                            await using var command1 = new NpgsqlCommand($"SELECT text from messages where message_id = '{message_id}'", connection);
                            await using var reader1 = await command1.ExecuteReaderAsync();
                            await reader1.ReadAsync();
                            Message sentMessage = await botClient.SendTextMessageAsync(
                                chatId: id,
                                text: $"Message deleted!\nChat: {update.DeletedBusinessMessages.Chat.Id}\nText: {reader1.GetString(0)}",
                                cancellationToken: cancellationToken);
                        }
                    }

                    if (update.EditedBusinessMessage != null)
                    {
                        if (update.EditedBusinessMessage.Text == null)
                        {
                            return;
                        }
                        await using var command0 = new NpgsqlCommand($"SELECT user_chat_id from connections where id = '{update.EditedBusinessMessage.BusinessConnectionId}'", connection);
                        await using var reader0 = await command0.ExecuteReaderAsync();
                        await reader0.ReadAsync();
                        string id = reader0.GetString(0);
                        await reader0.CloseAsync();
                        Console.WriteLine("New edited message!");


                        await using var command1 = new NpgsqlCommand($"SELECT text from messages where message_id = '{update.EditedBusinessMessage.MessageId}'", connection);
                        await using var reader1 = await command1.ExecuteReaderAsync();
                        await reader1.ReadAsync();
                        Message sentMessage = await botClient.SendTextMessageAsync(
                            chatId: id,
                            text: $"Message edited!\nChat: {update.EditedBusinessMessage.Chat.Id}\nOld text: {reader1.GetString(0)}\nNew text: {update.EditedBusinessMessage.Text}",
                            cancellationToken: cancellationToken);

                    }
                } catch (Exception e)
                {
                    Console.WriteLine(e);
                }
                


            }

            Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
            {
                var ErrorMessage = exception switch
                {
                    ApiRequestException apiRequestException
                        => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
                    _ => exception.ToString()
                };

                Console.WriteLine(ErrorMessage);
                return Task.CompletedTask;
            }
        }

    }
}
