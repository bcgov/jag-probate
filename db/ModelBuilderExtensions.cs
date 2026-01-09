using System;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;

namespace Probate.Db
{
    public static class ModelBuilderExtensions
    {
        public static void ApplyAllConfigurations(this ModelBuilder modelBuilder)
        {
            var applyConfigurationMethod = typeof(ModelBuilder)
                .GetMethods()
                .First(m =>
                    m.Name == nameof(ModelBuilder.ApplyConfiguration)
                    && m.GetParameters()[0].ParameterType.GetGenericTypeDefinition()
                        == typeof(IEntityTypeConfiguration<>)
                );

            var assembly = Assembly.GetExecutingAssembly();
            var entityConfigurationTypes = assembly
                .GetTypes()
                .Where(t =>
                    t.GetInterfaces()
                        .Any(i =>
                            i.IsGenericType
                            && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                        )
                );

            foreach (var type in entityConfigurationTypes)
            {
                var entityType = type.GetInterfaces()
                    .First(i =>
                        i.IsGenericType
                        && i.GetGenericTypeDefinition() == typeof(IEntityTypeConfiguration<>)
                    )
                    .GetGenericArguments()[0];

                var configurationInstance = Activator.CreateInstance(type);
                applyConfigurationMethod
                    .MakeGenericMethod(entityType)
                    .Invoke(modelBuilder, new[] { configurationInstance });
            }
        }
    }
}
