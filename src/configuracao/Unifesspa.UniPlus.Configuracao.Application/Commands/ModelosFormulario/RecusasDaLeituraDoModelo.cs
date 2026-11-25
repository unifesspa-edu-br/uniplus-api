namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class RecusasDaLeituraDoModelo
{
    public const string MensagemDaEntradaNula = "A página, o item, o grupo ou o termo veio nulo.";

    public List<FieldError> Recusas { get; } = [];

    public List<(int Indice, TParte Parte)> Lista<TEntrada, TParte>(
        IReadOnlyList<TEntrada?>? entradas, string nomeDaLista, Func<TEntrada, string, TParte?> ler)
        where TEntrada : class
        where TParte : class
    {
        List<(int, TParte)> lidas = [];
        foreach ((int indice, TEntrada? entrada) in (entradas ?? []).Index())
        {
            if (LerPresente(entrada, $"{nomeDaLista}[{indice}]", ler) is { } parte)
            {
                lidas.Add((indice, parte));
            }
        }

        return lidas;
    }

    public TParte? LerPresente<TEntrada, TParte>(TEntrada? entrada, string campo, Func<TEntrada, string, TParte?> ler)
        where TEntrada : class
        where TParte : class
    {
        if (entrada is null)
        {
            RecusarEntradaMalformada(campo, MensagemDaEntradaNula);
            return null;
        }

        return ler(entrada, campo);
    }

    public PredicadoDnf? LerPredicado(IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? entrada, string campo)
    {
        Result<PredicadoDnf?> predicado = EntradaDeRegras.Predicado(entrada);
        if (predicado.IsFailure)
        {
            Recusas.AddRange(predicado.Errors.Select(e => e.NoCaminhoDoItem(campo)));
        }

        return predicado.Value;
    }

    public void RecusarEntradaMalformada(string campo, string mensagem) =>
        Recusar(campo, new DomainError(ModeloFormularioErrorCodes.EntradaMalformada, mensagem));

    public bool RecusouDesde(int recusasAntes) => Recusas.Count > recusasAntes;

    public void Recusar(string campo, DomainError erro) => Recusas.Add(new(campo, erro));
}
