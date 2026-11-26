namespace Adhicom.Application.Common;

/// <summary>Résultat d'une opération applicative (succès ou erreur métier lisible par l'utilisateur).</summary>
public class OperationResult
{
    public bool Success { get; protected set; }
    public string? Error { get; protected set; }
    public static OperationResult Ok() => new() { Success = true };
    public static OperationResult Fail(string error) => new() { Success = false, Error = error };
}

public sealed class OperationResult<T> : OperationResult
{
    public T? Value { get; private set; }
    public static OperationResult<T> Ok(T value) => new() { Success = true, Value = value };
    public new static OperationResult<T> Fail(string error) => new() { Success = false, Error = error };
}
