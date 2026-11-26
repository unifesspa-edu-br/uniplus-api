namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As causas das consequências declaradas, compartilhadas pela conferência e pelo painel.</summary>
internal static class CoerenciaDaConsequenciaDoDescumprimento
{
    internal static IEnumerable<FieldError> Causas(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
    [
        .. AcrescimoDasExigencias(configuracao),
        .. AcrescimoDosGrupos(configuracao),
        .. CoerenciaDasExigencias(configuracao, fatos),
        .. CoerenciaDosGrupos(configuracao, fatos),
    ];

    internal static IEnumerable<FieldError> AcrescimoDasExigencias(ConfiguracaoDeclarada configuracao) =>
        Posicoes(configuracao, "documentosExigidos.exigencias", configuracao.Exigencias.DocumentosExigidos.Count)
            .Where(indice => !Recusada(configuracao, $"documentosExigidos.exigencias[{indice}]") && AcrescimoNaoOferecido(configuracao, configuracao.Exigencias.DocumentosExigidos[indice].ConsequenciaIndeferimento))
            .Select(indice => Acrescimo($"documentosExigidos.exigencias[{indice}].consequenciaIndeferimento", "A exigência"));

    internal static IEnumerable<FieldError> AcrescimoDosGrupos(ConfiguracaoDeclarada configuracao) =>
        Grupos(configuracao).Where(par => !Recusada(configuracao, par.Caminho) && AcrescimoNaoOferecido(configuracao, par.Grupo.Consequencia))
            .Select(par => Acrescimo($"{par.Caminho}.consequencia", "O grupo"));

    internal static IEnumerable<FieldError> CoerenciaDasExigencias(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        Posicoes(configuracao, "documentosExigidos.exigencias", configuracao.Exigencias.DocumentosExigidos.Count)
            .SelectMany(indice => CoerenciaDaExigencia(configuracao, fatos, indice));

    internal static IEnumerable<FieldError> CoerenciaDosGrupos(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        Grupos(configuracao).SelectMany(par => CoerenciaDoGrupo(configuracao, fatos, par.Grupo, par.Caminho));

    private static IEnumerable<FieldError> CoerenciaDaExigencia(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, int indice)
    {
        DocumentoExigido exigencia = configuracao.Exigencias.DocumentosExigidos[indice];
        if (exigencia.ConsequenciaIndeferimento is not { } consequencia || Recusada(configuracao, $"documentosExigidos.exigencias[{indice}]") || Recusada(configuracao, "distribuicaoVagas"))
        {
            return [];
        }
        return Modalidades(configuracao).Where(modalidade => exigencia.PodeAlcancarModalidade(modalidade.Codigo, fatos))
            .Where(modalidade => Incoerente(consequencia, modalidade))
            .Select(modalidade => Incoerencia($"documentosExigidos.exigencias[{indice}].consequenciaIndeferimento", "DocumentoExigido", modalidade));
    }

    private static IEnumerable<FieldError> CoerenciaDoGrupo(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, RegraDeComprovacao grupo, string caminho)
    {
        if (grupo.Consequencia is not { } consequencia || Recusada(configuracao, caminho) || Recusada(configuracao, "distribuicaoVagas"))
        {
            return [];
        }
        return Modalidades(configuracao).Where(modalidade => grupo.PodeAlcancarModalidade(modalidade.Codigo, fatos))
            .Where(modalidade => Incoerente(consequencia, modalidade))
            .Select(modalidade => Incoerencia($"{caminho}.consequencia", "RegraDeComprovacao", modalidade));
    }

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> Grupos(ConfiguracaoDeclarada configuracao) =>
        Percorrer(configuracao.Exigencias.RegrasDeComprovacao.Where(static regra => regra.RegraQueContemId is null), "documentosExigidos.regrasDeComprovacao");

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> Percorrer(IEnumerable<RegraDeComprovacao> regras, string lista) =>
        regras.OrderBy(static regra => regra.Ordem).SelectMany((regra, indice) => DoGrupo(regra, $"{lista}[{indice}]"));

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> DoGrupo(RegraDeComprovacao regra, string caminho) =>
    [
        .. regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && regra.Consequencia is not null ? new[] { (regra, caminho) } : [],
        .. Percorrer(regra.Itens, $"{caminho}.itens"),
    ];

    private static IEnumerable<int> Posicoes(ConfiguracaoDeclarada configuracao, string lista, int quantidade) =>
        Enumerable.Range(0, quantidade).OrderBy(indice => configuracao.Origem.PosicaoDeOrigem(lista, indice));

    private static bool Recusada(ConfiguracaoDeclarada configuracao, string caminho) =>
        configuracao.Origem.PartesRecusadas.Any(parte => parte.Caminho == caminho || caminho.StartsWith(parte.Caminho + ".", StringComparison.Ordinal) || caminho.StartsWith(parte.Caminho + "[", StringComparison.Ordinal) || parte.Caminho.StartsWith(caminho + ".", StringComparison.Ordinal) || parte.Caminho.StartsWith(caminho + "[", StringComparison.Ordinal));

    internal static bool PodeAvaliarAcrescimo(ConfiguracaoDeclarada configuracao, string lista) =>
        !Recusada(configuracao, lista) && !Recusada(configuracao, "bonusRegional");

    internal static bool PodeAvaliarCoerencia(ConfiguracaoDeclarada configuracao, string lista) =>
        !Recusada(configuracao, lista) && !Recusada(configuracao, "distribuicaoVagas");

    private static bool AcrescimoNaoOferecido(ConfiguracaoDeclarada configuracao, ConsequenciaDoDescumprimento? consequencia) =>
        !Recusada(configuracao, "bonusRegional") && consequencia == ConsequenciaDoDescumprimento.RetiraOAcrescimo && configuracao.Vagas.BonusRegional is null;

    private static IEnumerable<ModalidadeSelecionada> Modalidades(ConfiguracaoDeclarada configuracao) =>
        configuracao.Vagas.DistribuicaoVagas.SelectMany(static distribuicao => distribuicao.Modalidades);

    private static bool Incoerente(ConsequenciaDoDescumprimento consequencia, ModalidadeSelecionada modalidade) =>
        modalidade.AcaoQuandoIndeferido is { } acao && !string.Equals(acao, consequencia.Codigo(), StringComparison.Ordinal);

    private static FieldError Acrescimo(string campo, string sujeito) => new(campo, new DomainError(
        "ConsequenciasDoDescumprimento.AcrescimoNaoOferecido", $"{sujeito} determina retirar o acréscimo, mas o Fator de Atenuação das Desigualdades de Acesso não é oferecido."));

    private static FieldError Incoerencia(string campo, string origem, ModalidadeSelecionada modalidade) => new(campo, new DomainError(
        $"{origem}.ConsequenciaIncoerenteComAcaoDaVaga", $"A consequência não corresponde à ação de indeferimento da modalidade {(string.IsNullOrWhiteSpace(modalidade.Descricao) ? "selecionada" : modalidade.Descricao)}."));
}
