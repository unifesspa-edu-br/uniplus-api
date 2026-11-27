namespace Unifesspa.UniPlus.Configuracao.Application.Commands.ModelosFormulario;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Configuracao.Domain.Services;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Entradas;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;

internal sealed class RecusasDaLeituraDoModelo
{
    public const string MensagemDaEntradaNula = "A página, o item, o grupo ou o termo veio nulo.";

    public List<FieldError> Recusas { get; } = [];

    public List<(int Indice, TParte Parte)> Lista<TEntrada, TParte>(
        IReadOnlyList<TEntrada?>? entradas, string nomeDaLista, Func<TEntrada, string, TParte?> ler)
        where TEntrada : class
        where TParte : class =>
        ListaComPosicao(entradas, nomeDaLista, (entrada, _, campo) => ler(entrada, campo));

    /// <summary>Como <see cref="Lista{TEntrada, TParte}"/>, para quem precisa da posição da entrada na lista, além do caminho dela.</summary>
    public List<(int Indice, TParte Parte)> ListaComPosicao<TEntrada, TParte>(
        IReadOnlyList<TEntrada?>? entradas, string nomeDaLista, Func<TEntrada, int, string, TParte?> ler)
        where TEntrada : class
        where TParte : class
    {
        List<(int, TParte)> lidas = [];
        foreach ((int indice, TEntrada? entrada) in (entradas ?? []).Index())
        {
            if (LerPresente(entrada, $"{nomeDaLista}[{indice}]", (presente, campo) => ler(presente, indice, campo)) is { } parte)
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

    /// <summary>
    /// A obrigatoriedade lida: o predicado malformado a recusa, e o código que não é SEMPRE, NUNCA ou QUANDO com
    /// predicado também. Nula quando recusada.
    /// </summary>
    public Obrigatoriedade? LerObrigatoriedade(
        string? codigo, IReadOnlyList<IReadOnlyList<CondicaoPrecondicaoInput>>? predicadoDeEntrada, string caminhoDoCodigo, string caminhoDoPredicado)
    {
        int recusasAntes = Recusas.Count;
        PredicadoDnf? predicado = LerPredicado(predicadoDeEntrada, caminhoDoPredicado);
        return RecusouDesde(recusasAntes) ? null : ObrigatoriedadeDoCodigo(codigo, predicado, caminhoDoCodigo);
    }

    private Obrigatoriedade? ObrigatoriedadeDoCodigo(string? codigo, PredicadoDnf? predicado, string caminho)
    {
        Obrigatoriedade? obrigatoriedade = EntradaDeRegras.Obrigatoriedade(codigo, predicado);
        if (obrigatoriedade is null)
        {
            Recusar(caminho, new DomainError(
                ModeloFormularioErrorCodes.ObrigatoriedadeInvalida,
                "A obrigatoriedade é SEMPRE ou NUNCA, sem predicado, ou QUANDO, com predicado."));
        }

        return obrigatoriedade;
    }

    /// <summary>As recusas, uma por campo, dos textos da parte que o banco não grava, sem repetir o texto enviado.</summary>
    public static IEnumerable<FieldError> TextosNaoGravaveis(IEnumerable<(string Campo, string? Texto)> textos) =>
        textos.SelectMany(static t => TextoGravavel.Recusar(t.Campo, t.Texto)).DistinctBy(static e => e.Field);

    public void RecusarEntradaMalformada(string campo, string mensagem) =>
        Recusar(campo, new DomainError(ModeloFormularioErrorCodes.EntradaMalformada, mensagem));

    public bool RecusouDesde(int recusasAntes) => Recusas.Count > recusasAntes;

    public void Recusar(string campo, DomainError erro) => Recusas.Add(new(campo, erro));
}
