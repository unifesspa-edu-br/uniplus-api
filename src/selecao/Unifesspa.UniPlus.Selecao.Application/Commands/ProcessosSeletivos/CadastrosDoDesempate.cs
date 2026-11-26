namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Domain.Entities;
using Domain.Enums;
using Domain.Interfaces;
using Domain.ProcessosSeletivos.Desempate;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;

/// <summary>
/// O que a definição dos critérios de desempate consulta fora do processo: o catálogo das regras de desempate, lido
/// uma vez para cada critério se resolver em memória, e, quando algum critério cita <c>DESEMPATE_CONDICAO_SOBRE_DADO</c>, os
/// fatos do candidato. A leitura acontece antes de o processo ser travado para a mutação.
/// </summary>
internal sealed class CadastrosDoDesempate
{
    private readonly Dictionary<(string Codigo, string Versao), RegraCatalogo> _regras;
    private readonly IRegraCatalogoReader _regraCatalogoReader;

    private CadastrosDoDesempate(
        Dictionary<(string Codigo, string Versao), RegraCatalogo> regras,
        FatosDoDesempate? fatos,
        IRegraCatalogoReader regraCatalogoReader)
    {
        _regras = regras;
        Fatos = fatos;
        _regraCatalogoReader = regraCatalogoReader;
    }

    /// <summary>Os fatos do candidato; ausentes quando nenhum critério cita um predicado de fato.</summary>
    public FatosDoDesempate? Fatos { get; }

    /// <summary>
    /// Lista vazia ou acima do teto não consulta nada: a lista acima do teto é recusada inteira pelo agregado, sem leitura
    /// item a item.
    /// </summary>
    public static async Task<CadastrosDoDesempate> LerAsync(
        IReadOnlyList<CriterioDesempateInput> criterios,
        IRegraCatalogoReader regraCatalogoReader,
        IFatoCandidatoReader fatoCandidatoReader,
        CancellationToken cancellationToken)
    {
        if (criterios.Count == 0 || RegrasDoDesempate.ConferirQuantidadeDeCriteriosDesempate(criterios.Count).Count > 0)
        {
            return new([], null, regraCatalogoReader);
        }

        FatosDoDesempate? fatos = criterios.Any(static c => c.Regra?.Codigo == RegraDeDesempate.CondicaoSobreDado.Codigo())
            ? await FatosDoDesempate.LerAsync(fatoCandidatoReader, cancellationToken).ConfigureAwait(false)
            : null;
        IReadOnlyList<RegraCatalogo> regras = await regraCatalogoReader
            .ListarPorTipoAsync(TipoRegra.CriterioDesempate, cancellationToken)
            .ConfigureAwait(false);

        return new(regras.ToDictionary(static r => (r.Codigo, r.Versao)), fatos, regraCatalogoReader);
    }

    /// <summary>
    /// Resolve todos os critérios e acumula (ADR-0125) as recusas de cada um. O critério que não chega a ser criado também
    /// segue, com os parâmetros que se montaram, para a conferência contra o processo.
    /// </summary>
    public async Task<CriteriosResolvidos> ResolverAsync(
        IReadOnlyList<CriterioDesempateInput> criterios, CancellationToken cancellationToken)
    {
        CriteriosResolvidos resolvidos = new();
        for (int indice = 0; indice < criterios.Count; indice++)
        {
            CriterioDesempateInput input = criterios[indice];
            (Result<CriterioDesempate> resultado, ParametrosCriterioDesempate? parametros) =
                await ResolverCriterioAsync(input, cancellationToken).ConfigureAwait(false);
            resolvidos.Acrescentar(indice, input.Ordem, resultado, parametros);
        }

        return resolvidos;
    }

    private async Task<(Result<CriterioDesempate> Resultado, ParametrosCriterioDesempate? Parametros)> ResolverCriterioAsync(
        CriterioDesempateInput input, CancellationToken cancellationToken)
    {
        Result<RegraCatalogo> regra = await ObterRegraAsync(input, cancellationToken).ConfigureAwait(false);
        if (regra.IsFailure)
        {
            return (RecusaDoCriterio(input.Ordem, regra.Errors), null);
        }

        Result<ParametrosCriterioDesempate> parametros = ParametrosDoDesempateDeclarados.Montar(
            input.Ordem, input.Parametros ?? ParametrosDoCriterioDesempateInput.Ausentes, regra.Value!.Codigo);
        return parametros.IsFailure
            ? (RecusaDoCriterio(input.Ordem, parametros.Errors), null)
            : (CriterioDesempate.Criar(input.Ordem, regra.Value.Referencia(), parametros.Value!, Fatos?.Vocabulario), parametros.Value);
    }

    // A recusa que sai antes de CriterioDesempate.Criar acumula a da ordem, que Criar confere e que não depende da regra.
    private static Result<CriterioDesempate> RecusaDoCriterio(int ordem, IEnumerable<FieldError> erros) =>
        Result<CriterioDesempate>.ValidationFailure([.. CriterioDesempate.ValidarOrdem(ordem), .. erros]);

    /// <summary>
    /// A regra que o critério referencia, no catálogo já lido. Fora dele, consulta a regra pelo código, só para
    /// distinguir a que não existe da que existe com outro tipo.
    /// </summary>
    private async Task<Result<RegraCatalogo>> ObterRegraAsync(CriterioDesempateInput input, CancellationToken cancellationToken)
    {
        string codigo = input.Regra?.Codigo ?? string.Empty;
        string versao = input.Regra?.Versao ?? string.Empty;
        if (_regras.TryGetValue((codigo, versao), out RegraCatalogo? doCatalogo))
        {
            return Result<RegraCatalogo>.Success(doCatalogo);
        }

        RegraCatalogo? regra = await _regraCatalogoReader.ObterAsync(codigo, versao, cancellationToken).ConfigureAwait(false);
        return regra switch
        {
            null => Recusa("Regra de desempate não encontrada no catálogo de regras.", "CriterioDesempate.RegraNaoEncontrada"),
            { Tipo: not TipoRegra.CriterioDesempate } => Recusa("A regra informada não é uma regra de desempate.", "CriterioDesempate.RegraTipoInvalido"),
            _ => Result<RegraCatalogo>.Success(regra),
        };
    }

    private static Result<RegraCatalogo> Recusa(string mensagem, string codigo) =>
        Result<RegraCatalogo>.ValidationFailure([new("regraCodigo", new DomainError(codigo, mensagem))]);
}

/// <summary>
/// Os critérios que o pedido declarou, resolvidos: os criados, a ordem e os parâmetros de cada um, inclusive os que
/// recusaram, e as recusas acumuladas, cada uma no campo do critério.
/// </summary>
internal sealed class CriteriosResolvidos
{
    private readonly List<CriterioDesempate> _criados = [];
    private readonly List<CriterioDesempateInformado> _informados = [];
    private readonly List<FieldError> _recusas = [];

    public IReadOnlyList<CriterioDesempate> Criados => _criados;

    public IReadOnlyList<CriterioDesempateInformado> Informados => _informados;

    public IReadOnlyList<FieldError> Recusas => _recusas;

    public void Acrescentar(int indice, int ordem, Result<CriterioDesempate> resultado, ParametrosCriterioDesempate? parametros)
    {
        _informados.Add(new CriterioDesempateInformado(ordem, parametros));
        if (resultado.IsSuccess)
        {
            _criados.Add(resultado.Value!);
            return;
        }

        _recusas.AddRange(resultado.Errors.Select(erro => RegrasDoDesempate.CampoDoCriterioDesempate(indice, erro)));
    }
}
