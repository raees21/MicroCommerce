using Microsoft.EntityFrameworkCore;
using MicroCommerce.Contracts.Identity;
using MicroCommerce.Infrastructure.Messaging;
using MicroCommerce.SharedKernel.Configuration;
using MicroCommerce.Users.Api.Data;

namespace MicroCommerce.Users.Api.Services;

public sealed class UserRegisteredConsumer(
    KafkaConsumer consumer,
    KafkaOptions kafkaOptions,
    IServiceScopeFactory scopeFactory) : BackgroundService
{
    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        consumer.ConsumeAsync<UserRegisteredIntegrationEvent>(
            kafkaOptions.Topics.UserRegistered,
            async (message, cancellationToken) =>
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<UserProfileDbContext>();

                var existing = await dbContext.UserProfiles
                    .SingleOrDefaultAsync(x => x.Id == message.UserId, cancellationToken);

                if (existing is null)
                {
                    dbContext.UserProfiles.Add(new UserProfileEntity
                    {
                        Id = message.UserId,
                        Email = message.Email,
                        FullName = message.FullName
                    });
                }
                else
                {
                    existing.Email = message.Email;
                    existing.FullName = message.FullName;
                }

                await dbContext.SaveChangesAsync(cancellationToken);
            },
            stoppingToken);
}
