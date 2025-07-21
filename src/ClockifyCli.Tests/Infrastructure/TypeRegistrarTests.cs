using ClockifyCli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ClockifyCli.Tests.Infrastructure;

[TestFixture]
public class TypeRegistrarTests
{
    private IServiceCollection serviceCollection;
    private TypeRegistrar typeRegistrar;

    [SetUp]
    public void SetUp()
    {
        serviceCollection = new ServiceCollection();
        typeRegistrar = new TypeRegistrar(serviceCollection);
    }

    [Test]
    public void Constructor_WithValidServiceCollection_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new TypeRegistrar(serviceCollection));
    }

    [Test]
    public void Constructor_WithNullServiceCollection_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TypeRegistrar(null));
    }

    [Test]
    public void Register_WithValidTypes_ShouldRegisterService()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var implementationType = typeof(TestService);

        // Act
        Assert.DoesNotThrow(() => typeRegistrar.Register(serviceType, implementationType));

        // Assert
        var serviceDescriptor = serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        Assert.That(serviceDescriptor, Is.Not.Null);
        Assert.That(serviceDescriptor.ImplementationType, Is.EqualTo(implementationType));
        Assert.That(serviceDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public void RegisterInstance_WithValidInstance_ShouldRegisterInstance()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var instance = new TestService();

        // Act
        Assert.DoesNotThrow(() => typeRegistrar.RegisterInstance(serviceType, instance));

        // Assert
        var serviceDescriptor = serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        Assert.That(serviceDescriptor, Is.Not.Null);
        Assert.That(serviceDescriptor.ImplementationInstance, Is.EqualTo(instance));
        Assert.That(serviceDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public void RegisterLazy_WithValidFactory_ShouldRegisterFactory()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var instance = new TestService();
        Func<object> factory = () => instance;

        // Act
        Assert.DoesNotThrow(() => typeRegistrar.RegisterLazy(serviceType, factory));

        // Assert
        var serviceDescriptor = serviceCollection.FirstOrDefault(s => s.ServiceType == serviceType);
        Assert.That(serviceDescriptor, Is.Not.Null);
        Assert.That(serviceDescriptor.ImplementationFactory, Is.Not.Null);
        Assert.That(serviceDescriptor.Lifetime, Is.EqualTo(ServiceLifetime.Singleton));
    }

    [Test]
    public void Build_ShouldReturnTypeResolver()
    {
        // Act
        var resolver = typeRegistrar.Build();

        // Assert
        Assert.That(resolver, Is.Not.Null);
        Assert.That(resolver, Is.TypeOf<TypeResolver>());
    }

    [Test]
    public void Build_WithRegisteredServices_ShouldCreateWorkingResolver()
    {
        // Arrange
        var serviceType = typeof(ITestService);
        var implementationType = typeof(TestService);
        typeRegistrar.Register(serviceType, implementationType);

        // Act
        var resolver = typeRegistrar.Build();
        var resolvedService = resolver.Resolve(serviceType);

        // Assert
        Assert.That(resolvedService, Is.Not.Null);
        Assert.That(resolvedService, Is.TypeOf<TestService>());
    }
}

// Test interfaces and classes for testing
public interface ITestService
{
    string GetMessage();
}

public class TestService : ITestService
{
    public string GetMessage() => "Test Message";
}
