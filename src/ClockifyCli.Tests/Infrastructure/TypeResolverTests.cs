using ClockifyCli.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using NUnit.Framework;

namespace ClockifyCli.Tests.Infrastructure;

[TestFixture]
public class TypeResolverTests
{
    private IServiceProvider serviceProvider;
    private TypeResolver typeResolver;

    [SetUp]
    public void SetUp()
    {
        var services = new ServiceCollection();
        services.AddSingleton<ITestService, TestService>();
        services.AddSingleton<TestConcreteService>();
        
        serviceProvider = services.BuildServiceProvider();
        typeResolver = new TypeResolver(serviceProvider);
    }

    [TearDown]
    public void TearDown()
    {
        typeResolver?.Dispose();
        (serviceProvider as IDisposable)?.Dispose();
    }

    [Test]
    public void Constructor_WithValidServiceProvider_ShouldCreateSuccessfully()
    {
        // Act & Assert
        Assert.DoesNotThrow(() => new TypeResolver(serviceProvider));
    }

    [Test]
    public void Constructor_WithNullServiceProvider_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => new TypeResolver(null));
    }

    [Test]
    public void Resolve_WithRegisteredInterface_ShouldReturnImplementation()
    {
        // Arrange
        var serviceType = typeof(ITestService);

        // Act
        var result = typeResolver.Resolve(serviceType);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<TestService>());
        Assert.That(result, Is.InstanceOf<ITestService>());
    }

    [Test]
    public void Resolve_WithRegisteredConcreteType_ShouldReturnInstance()
    {
        // Arrange
        var serviceType = typeof(TestConcreteService);

        // Act
        var result = typeResolver.Resolve(serviceType);

        // Assert
        Assert.That(result, Is.Not.Null);
        Assert.That(result, Is.TypeOf<TestConcreteService>());
    }

    [Test]
    public void Resolve_WithUnregisteredType_ShouldReturnNull()
    {
        // Arrange
        var unregisteredType = typeof(UnregisteredService);

        // Act
        var result = typeResolver.Resolve(unregisteredType);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Resolve_WithNullType_ShouldReturnNull()
    {
        // Act
        var result = typeResolver.Resolve(null);

        // Assert
        Assert.That(result, Is.Null);
    }

    [Test]
    public void Resolve_SameTypeTwice_ShouldReturnSameInstance()
    {
        // Arrange
        var serviceType = typeof(ITestService);

        // Act
        var result1 = typeResolver.Resolve(serviceType);
        var result2 = typeResolver.Resolve(serviceType);

        // Assert
        Assert.That(result1, Is.Not.Null);
        Assert.That(result2, Is.Not.Null);
        Assert.That(result1, Is.SameAs(result2)); // Singleton should return same instance
    }

    [Test]
    public void Dispose_ShouldDisposeServiceProvider()
    {
        // Arrange
        var disposableServices = new ServiceCollection();
        disposableServices.AddSingleton<DisposableTestService>();
        var disposableProvider = disposableServices.BuildServiceProvider();
        var resolver = new TypeResolver(disposableProvider);

        // Get the service to ensure it's created
        var service = resolver.Resolve(typeof(DisposableTestService)) as DisposableTestService;
        Assert.That(service, Is.Not.Null);
        Assert.That(service.IsDisposed, Is.False);

        // Act
        resolver.Dispose();

        // Assert
        // The service should be disposed when the provider is disposed
        Assert.That(service.IsDisposed, Is.True);
    }

    [Test]
    public void Dispose_WithNonDisposableServiceProvider_ShouldNotThrow()
    {
        // Arrange
        var nonDisposableProvider = new NonDisposableServiceProvider();
        var resolver = new TypeResolver(nonDisposableProvider);

        // Act & Assert
        Assert.DoesNotThrow(() => resolver.Dispose());
    }
}

// Test classes for testing
public class TestConcreteService
{
    public string GetValue() => "Concrete Value";
}

public class UnregisteredService
{
    public string GetValue() => "Unregistered";
}

public class DisposableTestService : IDisposable
{
    public bool IsDisposed { get; private set; }
    
    public void Dispose()
    {
        IsDisposed = true;
    }
}

public class NonDisposableServiceProvider : IServiceProvider
{
    public object? GetService(Type serviceType) => null;
}
