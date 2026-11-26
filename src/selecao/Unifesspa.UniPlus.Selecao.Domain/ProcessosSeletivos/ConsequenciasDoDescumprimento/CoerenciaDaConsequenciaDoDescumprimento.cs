namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.ConsequenciasDoDescumprimento;

using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Extensions;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Cronograma;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Etapas;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>As causas das consequências declaradas, compartilhadas pela conferência e pelo painel.</summary>
internal static class CoerenciaDaConsequenciaDoDescumprimento
{
    internal static IEnumerable<FieldError> Causas(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, CronogramaDoProcesso cronograma, EtapasDoProcesso etapas) =>
    [
        .. ConsequenciasDasBancas.Acrescimo(configuracao, cronograma, etapas),
        .. AcrescimoDasExigencias(configuracao),
        .. AcrescimoDosGrupos(configuracao),
        .. ConsequenciasDasBancas.SemModalidade(configuracao, fatos, cronograma, etapas),
        .. SemModalidadeDasExigencias(configuracao, fatos),
        .. SemModalidadeDosGrupos(configuracao, fatos),
        .. CoerenciaDasExigencias(configuracao, fatos),
        .. CoerenciaDosGrupos(configuracao, fatos),
        .. ConsequenciasDasBancas.SemAmpla(configuracao, fatos, cronograma, etapas),
        .. SemAmplaDasExigencias(configuracao, fatos),
        .. SemAmplaDosGrupos(configuracao, fatos),
        .. SemAmplaDasModalidades(configuracao),
    ];

    internal static IEnumerable<FieldError> AcrescimoDasExigencias(ConfiguracaoDeclarada configuracao) =>
        Posicoes(configuracao, "documentosExigidos.exigencias", configuracao.Exigencias.DocumentosExigidos.Count)
            .Where(indice => !Recusada(configuracao, $"documentosExigidos.exigencias[{indice}]") && AcrescimoNaoOferecido(configuracao, configuracao.Exigencias.DocumentosExigidos[indice].ConsequenciaDoDescumprimento))
            .Select(indice => Acrescimo($"documentosExigidos.exigencias[{indice}].consequenciaDoDescumprimento", "A exigência"));

    internal static IEnumerable<FieldError> AcrescimoDosGrupos(ConfiguracaoDeclarada configuracao) =>
        Grupos(configuracao).Where(par => !Recusada(configuracao, par.Caminho) && AcrescimoNaoOferecido(configuracao, par.Grupo.ConsequenciaDoDescumprimento))
            .Select(par => Acrescimo($"{par.Caminho}.consequenciaDoDescumprimento", "O grupo"));

    internal static IEnumerable<FieldError> CoerenciaDasExigencias(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        Posicoes(configuracao, "documentosExigidos.exigencias", configuracao.Exigencias.DocumentosExigidos.Count)
            .SelectMany(indice => CoerenciaDaExigencia(configuracao, fatos, indice));

    internal static IEnumerable<FieldError> CoerenciaDosGrupos(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        Grupos(configuracao).SelectMany(par => CoerenciaDoGrupo(configuracao, fatos, par.Grupo, par.Caminho));

    internal static IEnumerable<FieldError> SemModalidadeDasExigencias(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        ExigenciasComRetirada(configuracao).Where(par => SemModalidade(configuracao, modalidade => par.Exigencia.PodeAlcancarModalidade(modalidade, fatos)))
            .Select(par => SemModalidade(par.Caminho));

    internal static IEnumerable<FieldError> SemModalidadeDosGrupos(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        GruposComRetirada(configuracao).Where(par => SemModalidade(configuracao, modalidade => par.Grupo.PodeAlcancarModalidade(modalidade, fatos)))
            .Select(par => SemModalidade($"{par.Caminho}.consequenciaDoDescumprimento"));

    internal static IEnumerable<FieldError> SemAmplaDasExigencias(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        ExigenciasComRetirada(configuracao).Where(par => SemAmpla(configuracao, modalidade => par.Exigencia.PodeAlcancarModalidade(modalidade, fatos)))
            .Select(par => SemAmpla(par.Caminho));

    internal static IEnumerable<FieldError> SemAmplaDosGrupos(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos) =>
        GruposComRetirada(configuracao).Where(par => SemAmpla(configuracao, modalidade => par.Grupo.PodeAlcancarModalidade(modalidade, fatos)))
            .Select(par => SemAmpla($"{par.Caminho}.consequenciaDoDescumprimento"));

    private static IEnumerable<(DocumentoExigido Exigencia, string Caminho)> ExigenciasComRetirada(ConfiguracaoDeclarada configuracao) =>
        Posicoes(configuracao, "documentosExigidos.exigencias", configuracao.Exigencias.DocumentosExigidos.Count)
            .Where(indice => !Recusada(configuracao, $"documentosExigidos.exigencias[{indice}]") && !Recusada(configuracao, "distribuicaoVagas"))
            .Select(indice => (Exigencia: configuracao.Exigencias.DocumentosExigidos[indice], Caminho: $"documentosExigidos.exigencias[{indice}].consequenciaDoDescumprimento"))
            .Where(par => par.Exigencia.ConsequenciaDoDescumprimento == ConsequenciaDoDescumprimento.RetiraAModalidade);

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> GruposComRetirada(ConfiguracaoDeclarada configuracao) =>
        Grupos(configuracao).Where(par => !Recusada(configuracao, par.Caminho) && !Recusada(configuracao, "distribuicaoVagas")
            && par.Grupo.ConsequenciaDoDescumprimento == ConsequenciaDoDescumprimento.RetiraAModalidade);

    internal static bool SemModalidade(ConfiguracaoDeclarada configuracao, Func<string, bool> alcanca)
    {
        string[] modalidades = [.. Modalidades(configuracao).Select(static modalidade => modalidade.Codigo).Distinct(StringComparer.Ordinal)];
        int alcancadas = modalidades.Count(alcanca);
        return alcancadas == 0 || alcancadas == modalidades.Length;
    }

    internal static bool SemAmpla(ConfiguracaoDeclarada configuracao, Func<string, bool> alcanca) =>
        configuracao.Vagas.DistribuicaoVagas.Any(oferta => oferta.Modalidades.Any(modalidade => alcanca(modalidade.Codigo))
            && !oferta.Modalidades.Any(static modalidade => modalidade.Codigo == "AC"));

    internal static IEnumerable<FieldError> SemAmplaDasModalidades(ConfiguracaoDeclarada configuracao) =>
        Recusada(configuracao, "distribuicaoVagas") ? [] : configuracao.Vagas.DistribuicaoVagas
            .SelectMany((oferta, indice) => oferta.Modalidades.Any(static modalidade => modalidade.Codigo == "AC") ? []
                : oferta.Modalidades.Where(static modalidade => modalidade.AcaoQuandoIndeferido == "RETIRA_A_MODALIDADE")
                    .Select(_ => SemAmpla($"distribuicaoVagas[{indice}].modalidades")));

    internal static FieldError SemModalidade(string campo) => new(campo, new DomainError(
        "ConsequenciasDoDescumprimento.SemModalidadeEmQuestao", "A retirada da modalidade exige uma condição que alcance algumas, mas não todas, as modalidades ofertadas."));

    internal static FieldError SemAmpla(string campo) => new(campo, new DomainError(
        "ConsequenciasDoDescumprimento.RetiraAModalidadeSemAmplaConcorrencia", "A retirada da modalidade exige ampla concorrência na oferta."));

    private static IEnumerable<FieldError> CoerenciaDaExigencia(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, int indice)
    {
        DocumentoExigido exigencia = configuracao.Exigencias.DocumentosExigidos[indice];
        if (exigencia.ConsequenciaDoDescumprimento is not { } consequencia || Recusada(configuracao, $"documentosExigidos.exigencias[{indice}]") || Recusada(configuracao, "distribuicaoVagas"))
        {
            return [];
        }
        return Modalidades(configuracao).Where(modalidade => exigencia.PodeAlcancarModalidade(modalidade.Codigo, fatos))
            .Where(modalidade => Incoerente(consequencia, modalidade))
            .Select(modalidade => Incoerencia($"documentosExigidos.exigencias[{indice}].consequenciaDoDescumprimento", "DocumentoExigido", modalidade));
    }

    private static IEnumerable<FieldError> CoerenciaDoGrupo(ConfiguracaoDeclarada configuracao, FatosDeModalidade fatos, RegraDeComprovacao grupo, string caminho)
    {
        if (grupo.ConsequenciaDoDescumprimento is not { } consequencia || Recusada(configuracao, caminho) || Recusada(configuracao, "distribuicaoVagas"))
        {
            return [];
        }
        return Modalidades(configuracao).Where(modalidade => grupo.PodeAlcancarModalidade(modalidade.Codigo, fatos))
            .Where(modalidade => Incoerente(consequencia, modalidade))
            .Select(modalidade => Incoerencia($"{caminho}.consequenciaDoDescumprimento", "RegraDeComprovacao", modalidade));
    }

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> Grupos(ConfiguracaoDeclarada configuracao) =>
        Percorrer(configuracao, configuracao.Exigencias.RegrasDeComprovacao.Where(static regra => regra.RegraQueContemId is null), "documentosExigidos.regrasDeComprovacao");

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> Percorrer(ConfiguracaoDeclarada configuracao, IEnumerable<RegraDeComprovacao> regras, string lista)
    {
        RegraDeComprovacao[] ordenadas = [.. regras.OrderBy(static regra => regra.Ordem)];
        return Posicoes(configuracao, lista, ordenadas.Length).SelectMany(indice => DoGrupo(configuracao, ordenadas[indice], $"{lista}[{indice}]"));
    }

    private static IEnumerable<(RegraDeComprovacao Grupo, string Caminho)> DoGrupo(ConfiguracaoDeclarada configuracao, RegraDeComprovacao regra, string caminho) =>
    [
        .. regra.Forma == FormaDeComprovacao.QuantidadeMinimaDeItens && regra.ConsequenciaDoDescumprimento is not null ? new[] { (regra, caminho) } : [],
        .. Percorrer(configuracao, regra.Itens, $"{caminho}.itens"),
    ];

    private static IEnumerable<int> Posicoes(ConfiguracaoDeclarada configuracao, string lista, int quantidade) =>
        Enumerable.Range(0, quantidade).OrderBy(indice => configuracao.Origem.PosicaoDeOrigem(lista, indice));

    internal static bool Recusada(ConfiguracaoDeclarada configuracao, string caminho) =>
        configuracao.Origem.PartesRecusadas.Any(parte => parte.Caminho == caminho || caminho.StartsWith(parte.Caminho + ".", StringComparison.Ordinal) || caminho.StartsWith(parte.Caminho + "[", StringComparison.Ordinal) || parte.Caminho.StartsWith(caminho + ".", StringComparison.Ordinal) || parte.Caminho.StartsWith(caminho + "[", StringComparison.Ordinal));

    internal static bool PodeAvaliarAcrescimo(ConfiguracaoDeclarada configuracao, string lista) =>
        !Recusada(configuracao, lista) && !Recusada(configuracao, "bonusRegional");

    internal static bool PodeAvaliarCoerencia(ConfiguracaoDeclarada configuracao, string lista) =>
        !Recusada(configuracao, lista) && !Recusada(configuracao, "distribuicaoVagas");

    internal static bool AcrescimoNaoOferecido(ConfiguracaoDeclarada configuracao, ConsequenciaDoDescumprimento? consequencia) =>
        !Recusada(configuracao, "bonusRegional") && consequencia == ConsequenciaDoDescumprimento.RetiraOAcrescimo && configuracao.Vagas.BonusRegional is null;

    private static IEnumerable<ModalidadeSelecionada> Modalidades(ConfiguracaoDeclarada configuracao) =>
        configuracao.Vagas.DistribuicaoVagas.SelectMany(static distribuicao => distribuicao.Modalidades);

    private static bool Incoerente(ConsequenciaDoDescumprimento consequencia, ModalidadeSelecionada modalidade) =>
        modalidade.AcaoQuandoIndeferido is { } acao && !string.Equals(acao, consequencia.Codigo(), StringComparison.Ordinal);

    internal static FieldError Acrescimo(string campo, string sujeito) => new(campo, new DomainError(
        "ConsequenciasDoDescumprimento.AcrescimoNaoOferecido", $"{sujeito} determina retirar o acréscimo, mas o Fator de Atenuação das Desigualdades de Acesso não é oferecido."));

    private static FieldError Incoerencia(string campo, string origem, ModalidadeSelecionada modalidade) => new(campo, new DomainError(
        $"{origem}.ConsequenciaIncoerenteComAcaoDaVaga", $"A consequência não corresponde à ação de indeferimento da modalidade {(string.IsNullOrWhiteSpace(modalidade.Descricao) ? "selecionada" : modalidade.Descricao)}."));
}
