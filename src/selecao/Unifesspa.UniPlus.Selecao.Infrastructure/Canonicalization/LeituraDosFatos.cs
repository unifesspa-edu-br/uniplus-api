namespace Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

using Unifesspa.UniPlus.Kernel.Domain.Cidades;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Formularios;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

using LinhaDoPredicado = (int Clausula, string Fato, Unifesspa.UniPlus.Regras.Enums.Operador Operador, System.Text.Json.JsonElement Valor);

/// <summary>
/// Leitura dos fatos coletados e dos grupos de coleta.
/// </summary>
internal static class LeituraDosFatos
{
    private static readonly string[] ChavesDoFatoColetado =
    [
        "codigoDoDado", "formularioCodigo", "paginaCodigo", "ordem", "rotulo", "tipoDeCampo", "obrigatoriedade", "ajuda",
        "pedirConfirmacao", "restricoes", "impedimento", "origemValores", "formato", "classificacaoProtecao",
        "hipoteseLegal", "finalidadeTratamento", "descricao", "componenteExibicao", "exibicao", "valoresSelecionaveis",
    ];

    /// <summary>
    /// A política de referência temporal como foi congelada, insumo de <see cref="Entities.ProcessoSeletivo.ResolverDataReferenciaFatos"/>.
    /// </summary>
    internal static ReferenciaTemporalFatos? LerReferenciaTemporalFatosPolitica(LeitorEnvelope leitor, JsonObject bloco)
    {
        JsonObject? json = leitor.ObjetoOpcional(bloco, "referenciaTemporalDosDados", "documentosExigidos");
        if (leitor.Falhou || json is null)
        {
            return null;
        }

        const string path = "documentosExigidos.referenciaTemporalDosDados";
        leitor.ExigirChaves(json, path, "tipo", "data", "faseId");
        string tipoCodigo = leitor.TextoNaoVazio(json, "tipo", path);
        DateOnly? data = leitor.DataOpcional(json, "data", path);
        Guid? faseId = leitor.IdentificadorOpcional(json, "faseId", path);
        return leitor.Falhou ? null : CriarReferenciaTemporal(leitor, tipoCodigo, data, faseId, path);
    }

    private static ReferenciaTemporalFatos? CriarReferenciaTemporal(LeitorEnvelope leitor, string tipoCodigo, DateOnly? data, Guid? faseId, string caminho)
    {
        Result<ReferenciaTemporalFatos> resultado = ReferenciaTemporalFatos.Criar(tipoCodigo, data, faseId);
        return resultado.IsFailure ? leitor.Propagar<ReferenciaTemporalFatos>(resultado.Errors, caminho) : resultado.Value;
    }

    /// <summary>
    /// Os fatos coletados, reconstruídos por <see cref="FatoColetado.Criar"/>
    /// (que revalida a forma: código, ordem, auto-referência), Ids novos (não são congelados no
    /// envelope, diferente de <c>etapa.Id</c>). A pré-condição é a mesma forma DNF do gatilho.
    /// <c>ValoresSelecionaveis</c> (UNI-REQ-0072) é o dicionário reidratado —
    /// completo, uma entrada por fato coletado (array para os de seleção, <see langword="null"/>
    /// para os demais) — que acompanha o envelope para recanonicalizar, sem reconsultar o
    /// catálogo vivo.
    /// </summary>
    internal static (
        IReadOnlyList<FatoColetado> Fatos,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> ValoresSelecionaveis)
        LerFatosColetados(LeitorEnvelope leitor, JsonObject payload, FormulariosDoDocumento formularios)
    {
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis = new(StringComparer.Ordinal);

        JsonArray array = leitor.Array(payload, "dadosDosFormularios", "$");
        return leitor.Falhou ? ([], valoresSelecionaveis) : (LerListaDeFatos(leitor, array, "dadosDosFormularios", valoresSelecionaveis, formularios), valoresSelecionaveis);
    }

    /// <summary>Uma lista de fatos coletados — os itens ou os campos de um grupo —, na forma congelada.</summary>
    internal static IReadOnlyList<FatoColetado> LerListaDeFatos(
        LeitorEnvelope leitor,
        JsonArray array,
        string caminho,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        FormulariosDoDocumento formularios)
    {
        List<FatoColetado> fatos = [];
        for (int i = 0; i < array.Count; i++)
        {
            FatoColetado? fato = LerFatoColetado(
                leitor, leitor.ItemObjeto(array, i, caminho), $"{caminho}[{i}]", valoresSelecionaveis, formularios);
            if (fato is null)
            {
                return [];
            }

            fatos.Add(fato);
        }

        return fatos;
    }

    /// <summary>
    /// A unicidade do código do fato é reconferida pela conferência do bloco de fatos mais adiante:
    /// um envelope adulterado com fato duplicado sobrescreve a entrada de <paramref name="valoresSelecionaveis"/>
    /// aqui e é recusado lá. Todas as recusas do item saem juntas, na ordem: vocabulário, regras (exibição,
    /// obrigatoriedade, restrições, impedimento), forma do domínio e, por último, a recusa de formato que encerrou a leitura.
    /// </summary>
    private static FatoColetado? LerFatoColetado(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis,
        FormulariosDoDocumento formularios)
    {
        leitor.ExigirChaves(item, path, ChavesDoFatoColetado);
        DeclaracaoDoFato declaracaoDoFato = LerDeclaracao(leitor, item, path, formularios);
        if (leitor.Falhou)
        {
            return null;
        }

        RegrasDoItemLidas regras = LerRegrasDoItem(leitor, item, path);
        IReadOnlyList<FieldError> recusas =
            [.. declaracaoDoFato.RecusasDoVocabulario(path), .. regras.Recusas, .. ImpedimentoForaDaInscricao(declaracaoDoFato, regras.Impedimento?.Value, path, formularios)];
        return leitor.Falhou ? PropagarSeHouver(leitor, recusas, path) : CriarOuRecusar(leitor, item, path, (declaracaoDoFato, regras, recusas), valoresSelecionaveis);
    }

    private static FatoColetado? CriarOuRecusar(
        LeitorEnvelope leitor,
        JsonObject item,
        string path,
        (DeclaracaoDoFato Declaracao, RegrasDoItemLidas Regras, IReadOnlyList<FieldError> Recusas) lido,
        Dictionary<string, IReadOnlyList<ValorDominioDeclaradoCongelado>?> valoresSelecionaveis)
    {
        (FatoColetado? fato, IReadOnlyList<FieldError> daForma) = ConferirAForma(lido.Declaracao, lido.Regras);
        IReadOnlyList<ValorDominioDeclaradoCongelado>? valores = lido.Declaracao.TipoDeCampo is { } tipo
            ? LeituraDosValoresDeDominio.LerValoresSelecionaveis(leitor, item, path, tipo)
            : null;
        IReadOnlyList<FieldError> causas = [.. lido.Recusas, .. CausasDoFatoNoDocumento(daForma)];
        if (causas.Count > 0 || leitor.Falhou || fato is null)
        {
            return PropagarSeHouver(leitor, causas, path);
        }

        valoresSelecionaveis[lido.Declaracao.FatoCodigo] = valores;
        return fato;
    }

    /// <summary>
    /// A forma do item pelo domínio. Com todas as partes lidas, é a criação do item; com parte recusada, é a conferência
    /// das partes lidas, que não acusa a parte recusada. Sem o tipo de campo, que a forma exige, nada é conferido.
    /// </summary>
    private static (FatoColetado? Fato, IReadOnlyList<FieldError> Recusas) ConferirAForma(DeclaracaoDoFato declaracaoDoFato, RegrasDoItemLidas regras)
    {
        if (declaracaoDoFato.TipoDeCampo is not { } tipo)
        {
            return (null, []);
        }

        ApresentacaoDoCampo apresentacao = new(declaracaoDoFato.Rotulo, tipo, declaracaoDoFato.Ajuda, declaracaoDoFato.PedirConfirmacao);
        if (regras.Completas is not { } completas)
        {
            return (null, FatoColetado.ConferirForma(declaracaoDoFato.FatoCodigo, declaracaoDoFato.Ordem, apresentacao, regras.Lidas, declaracaoDoFato.CopiaParaAForma));
        }

        Result<FatoColetado> criado = FatoColetado.Criar(declaracaoDoFato.FatoCodigo, declaracaoDoFato.Ordem, declaracaoDoFato.PaginaCodigo, apresentacao, completas, declaracaoDoFato.CopiaParaAForma, declaracaoDoFato.Formulario);
        return (criado.IsSuccess ? criado.Value : null, criado.Errors);
    }

    private static FatoColetado? PropagarSeHouver(LeitorEnvelope leitor, IReadOnlyList<FieldError> causas, string path) =>
        causas.Count > 0 ? leitor.PropagarCausasDoItem<FatoColetado?>(causas, path) : null;

    private static IReadOnlyList<FieldError> CausasDoFatoNoDocumento(IReadOnlyList<FieldError> causas) =>
        [.. causas.Select(static causa => causa.Field is "ordem" or "formato" or "classificacaoProtecao" or "componenteExibicao"
            ? causa : LeitorEnvelope.NoProprioItem([causa])[0])];

    private static RegrasDoItemLidas LerRegrasDoItem(LeitorEnvelope leitor, JsonObject item, string path)
    {
        Result<PredicadoDnf?>? exibicao = LerExibicao(leitor, item, path);
        Result<Obrigatoriedade>? obrigatoriedade = leitor.Falhou ? null : LeituraDasCondicoes.LerObrigatoriedade(leitor, item, path);
        IReadOnlyList<Result<RestricaoValor>> restricoes = leitor.Falhou ? [] : LeituraDasRestricoes.LerRestricoes(leitor, item, path);
        Result<Impedimento?>? impedimento = leitor.Falhou ? null : LeituraDasCondicoes.LerImpedimento(leitor, item, path);
        return new RegrasDoItemLidas(exibicao, obrigatoriedade, restricoes, impedimento);
    }

    private static Result<PredicadoDnf?>? LerExibicao(LeitorEnvelope leitor, JsonObject item, string path)
    {
        IReadOnlyList<LinhaDoPredicado> linhas = LeituraDasCondicoes.LerDnf(leitor, item, "exibicao", path);
        return leitor.Falhou ? null : LeituraDasCondicoes.PredicadoOpcional(linhas);
    }

    /// <summary>
    /// A forma do item não conhece o formulário; o agregado recusa o impedimento fora da inscrição ao definir os itens, e o
    /// decoder tem de recusar o mesmo. Sem formulário lido, não há o que conferir.
    /// </summary>
    private static IEnumerable<FieldError> ImpedimentoForaDaInscricao(
        DeclaracaoDoFato declaracaoDoFato, Impedimento? impedimento, string path, FormulariosDoDocumento formularios) =>
        declaracaoDoFato.Formulario is null || impedimento is null
            ? []
            : Impedimento.ConferirDeclaracao(formularios.DeclaracaoDe(declaracaoDoFato.Formulario), [impedimento]).Select(recusa => new FieldError(
                "impedimento", new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.impedimento': {recusa.Error.Message}")));

    /// <summary>
    /// A exibição, a obrigatoriedade, as restrições e o impedimento lidos do item. A recusa de regra de cada parte fica no
    /// resultado dela, e a parte não lida, por recusa de formato, é nula; as restrições são só as lidas antes dessa recusa.
    /// </summary>
    private sealed record RegrasDoItemLidas(
        Result<PredicadoDnf?>? Exibicao,
        Result<Obrigatoriedade>? Obrigatoriedade,
        IReadOnlyList<Result<RestricaoValor>> Restricoes,
        Result<Impedimento?>? Impedimento)
    {
        /// <summary>As recusas de regra, no caminho relativo ao item, na ordem das regras.</summary>
        public IReadOnlyList<FieldError> Recusas =>
        [
            .. (Exibicao?.Errors ?? []).Select(static causa => causa.NoCaminhoDoItem("exibicao")),
            .. Obrigatoriedade?.Errors ?? [],
            .. Restricoes.SelectMany(static (restricao, indice) => restricao.Errors.Select(causa => causa.NoCaminhoDoItem($"restricoes[{indice}]"))),
            .. Impedimento?.Errors ?? [],
        ];

        /// <summary>As regras lidas; a parte recusada ou não lida fica nula e conta como conforme.</summary>
        public RegrasLidasDoCampo Lidas => new(
            Exibicao?.Value,
            Obrigatoriedade?.Value,
            [.. Restricoes.Select(static restricao => restricao.IsSuccess ? restricao.Value : null)],
            Impedimento?.Value);

        /// <summary>As regras do item quando todas as partes foram lidas; nulo quando alguma foi recusada ou não lida.</summary>
        public RegrasDoCampo? Completas =>
            Recusas.Count == 0 && Exibicao is not null && Obrigatoriedade is not null && Impedimento is not null
                ? new RegrasDoCampo(Exibicao.Value, Obrigatoriedade.Value!, [.. Restricoes.Select(static restricao => restricao.Value!)], Impedimento.Value)
                : null;
    }

    private static DeclaracaoDoFato LerDeclaracao(LeitorEnvelope leitor, JsonObject item, string path, FormulariosDoDocumento formularios) => new()
    {
        FatoCodigo = leitor.TextoNaoVazio(item, "codigoDoDado", path, LimitesDoEnvelope.Fato),
        Formulario = formularios.De(leitor.TextoNaoVazio(item, "formularioCodigo", path, CodigoDoFormulario.TamanhoMaximo)),
        PaginaCodigo = leitor.TextoOpcional(item, "paginaCodigo", path, LimitesDoEnvelope.CodigoPaginaFormulario),
        Ordem = leitor.Inteiro(item, "ordem", path),
        Rotulo = leitor.TextoNaoVazio(item, "rotulo", path, LimitesDoEnvelope.NomeDeCadastro),
        TipoDeCampo = TipoDoCodigo(leitor.TextoNaoVazio(item, "tipoDeCampo", path)),
        Ajuda = leitor.TextoOpcional(item, "ajuda", path, LimitesDoEnvelope.AjudaDoCampo),
        PedirConfirmacao = leitor.Booleano(item, "pedirConfirmacao", path),
        Copia = LerCopiaDoCatalogo(leitor, item, path),
    };

    private static CopiaDoItemNoCatalogo LerCopiaDoCatalogo(LeitorEnvelope leitor, JsonObject item, string path) => new(
        leitor.CodigoDeContrato<OrigemValoresColeta>(item, "origemValores", path),
        leitor.TextoOpcional(item, "formato", path, LimitesDoEnvelope.Token),
        leitor.TextoNaoVazio(item, "classificacaoProtecao", path, LimitesDoEnvelope.Token),
        leitor.TextoNaoVazio(item, "hipoteseLegal", path, LimitesDoEnvelope.HipoteseLegal),
        leitor.TextoNaoVazio(item, "finalidadeTratamento", path, LimitesDoEnvelope.FinalidadeTratamento),
        leitor.TextoNaoVazio(item, "descricao", path, LimitesDoEnvelope.DescricaoDoFato),
        leitor.TextoOpcional(item, "componenteExibicao", path, LimitesDoEnvelope.ComponenteExibicao));

    private static TipoDeCampo? TipoDoCodigo(string codigo) =>
        CatalogoDe<TipoDeCampo>.TentarObter(codigo, out TipoDeCampo tipo) ? tipo : null;

    private sealed class DeclaracaoDoFato
    {
        public string FatoCodigo { get; init; } = string.Empty;

        public FormularioProcesso? Formulario { get; init; }

        public string? PaginaCodigo { get; init; }

        public int Ordem { get; init; }

        public string Rotulo { get; init; } = string.Empty;

        public TipoDeCampo? TipoDeCampo { get; init; }

        public string? Ajuda { get; init; }

        public bool PedirConfirmacao { get; init; }

        public required CopiaDoItemNoCatalogo Copia { get; init; }

        /// <summary>
        /// A cópia do catálogo para o domínio conferir. O componente fora da forma canônica já tem a recusa do vocabulário,
        /// e o domínio não a repete.
        /// </summary>
        public CopiaDoItemNoCatalogo CopiaParaAForma => ComponenteForaDaFormaCanonica ? Copia with { ComponenteExibicao = null } : Copia;

        private bool ComponenteForaDaFormaCanonica =>
            Copia.ComponenteExibicao is { } componente && (string.IsNullOrWhiteSpace(componente) || componente != componente.Trim());

        /// <summary>As quatro conferências do vocabulário são independentes, e todas as recusas saem juntas.</summary>
        public IEnumerable<FieldError> RecusasDoVocabulario(string path) =>
        [
            .. Recusa(!CatalogoDe<HipoteseLegalTratamento>.TentarObter(Copia.HipoteseLegal, out _), "hipoteseLegal", path, "fora do vocabulário de hipóteses legais."),
            .. Recusa(ComponenteForaDaFormaCanonica, "componenteExibicao", path, "não está na forma canônica: sem espaço nas pontas e não vazio."),
            .. Recusa(Formulario is null, "formularioCodigo", path, "não é o código de um formulário do documento."),
            .. Recusa(TipoDeCampo is null, "tipoDeCampo", path, $"fora dos tipos de campo aceitos: {string.Join(", ", CatalogoDe<TipoDeCampo>.Todos().Select(static c => c.Codigo))}."),
        ];

        private static FieldError[] Recusa(bool recusado, string chave, string path, string motivo) =>
            recusado ? [new FieldError(chave, new DomainError(ErrosCodecEnvelope.EnvelopeMalformado, $"'{path}.{chave}' {motivo}"))] : [];
    }
}
