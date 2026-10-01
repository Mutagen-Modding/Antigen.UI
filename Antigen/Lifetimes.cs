namespace Antigen;

public interface ISingleton;

public interface ITransient;

/// <summary>One per profile, for as long as the profile exists.</summary>
public interface IProfileScoped;

/// <summary>One per activation of a profile.</summary>
public interface IActiveScoped;

public static class LifetimeScopes
{
    public const string Profile = nameof(Profile);
    public const string Active = nameof(Active);
}
