namespace Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

/// <summary>
/// Registro que compõe a ordem total da trilha da contenção do perfil. O agregado numera os registros novos na ordem em
/// que os cria (<see cref="OrdinalLocal"/>, que não é gravado); a persistência troca esse ordinal por um número de uma
/// sequence compartilhada, atribuído sob a trava do perfil, de modo que a ordem sobreviva a instantes iguais.
/// </summary>
public interface IRegistroDaTrilhaDeContencao
{
    /// <summary>A posição do registro entre os que o agregado criou, para ordenar os que entram numa mesma gravação.</summary>
    int OrdinalLocal { get; }

    /// <summary>O número da sequence compartilhada; zero até a gravação.</summary>
    long OrdemNaTrilha { get; }
}
