namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// Story #927 — a regra de derivação persistida na configuração do processo, e a sua reconstrução no
/// value object que o motor consome. A validação aqui é estrutural (unicidade, forma); a semântica
/// (fato alvo derivado, domínio de contribuição) é da Application.
/// </summary>
public sealed class ProcessoSeletivoRegrasDerivacaoTests
{
    private static readonly IReadOnlyCollection<string> DominioModalidade =
        RegrasDerivacaoModalidadeLei12711.DominioCanonico;

    private static ProcessoSeletivo NovoProcesso() =>
        ProcessoSeletivo.Criar("PS Derivação", TipoProcesso.SiSU, OrigemCandidatos.ImportacaoExterna);

    private static CondicaoRegraDerivacao Cond(string fato, bool valor) =>
        CondicaoRegraDerivacao.Criar(1, fato, Operador.Igual, JsonSerializer.SerializeToElement(valor)).Value!;

    private static RegraDerivacaoConfigurada Regra(int ordem, string contribui, params (string Fato, bool Valor)[] atomos) =>
        RegraDerivacaoConfigurada.Criar(ordem, contribui, [.. atomos.Select(a => Cond(a.Fato, a.Valor))]).Value!;

    private static RegraDerivacaoConfigurada Ancora(int ordem, string contribui) =>
        RegraDerivacaoConfigurada.Criar(ordem, contribui, condicoes: null).Value!;

    private static ConfiguracaoDerivacaoFato ConfigModalidade() =>
        ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
        [
            Ancora(0, "AC"),
            Regra(1, "AC_PCD", ("CONCORRER_PCD", true)),
            Regra(2, "LI_PCD", ("CONCORRER_PCD", true), ("EGRESSO_ESCOLA_PUBLICA", true)),
        ]).Value!;

    /// <summary>Oferta uma distribuição mínima cujas modalidades são o domínio congelado de contribuição.</summary>
    private static void OfertarModalidades(ProcessoSeletivo processo, params string[] codigos)
    {
        List<ModalidadeSelecionada> modalidades =
        [
            .. codigos.Select(static codigo => ModalidadeSelecionada.Criar(
                modalidadeOrigemId: Guid.CreateVersion7(),
                codigo: codigo,
                descricao: null,
                naturezaLegal: NaturezaLegalModalidade.Ampla,
                composicaoVagas: ComposicaoVagasModalidade.ResidualDoVo,
                composicaoOrigemCodigo: null,
                regraRemanejamento: RegraRemanejamentoModalidade.Nenhuma,
                remanejamentoDestino: null,
                remanejamentoPar: null,
                remanejamentoFallback: null,
                criteriosCumulativos: [],
                acaoQuandoIndeferido: null,
                baseLegal: "Res. Unifesspa 532/2021",
                quantidadeDeclarada: 10).Value!),
        ];

        ConfiguracaoDistribuicaoVagas config = ConfiguracaoDistribuicaoVagas.Criar(
            ofertaCursoOrigemId: Guid.CreateVersion7(),
            voBase: 40,
            pr: 1m,
            regraDistribuicao: ReferenciaRegra.Criar(RegraDistribuicaoVagasCodigo.Institucional, "v1", new string('a', 64)).Value!,
            regraAjuste: null,
            referenciaDemografica: null,
            modalidades: modalidades).Value!;

        processo.DefinirDistribuicaoVagas([config], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
    }

    /// <summary>
    /// Um ciclo de derivação (D1 depende de D2, D2 depende de D1) atravessa a validação isolada
    /// de <see cref="ProcessoSeletivo.DefinirRegrasDerivacao"/> — que só barra código duplicado.
    /// A aciclicidade do grafo conjunto (§7) é condição para congelar a ordem topológica (RN08),
    /// e o gate de pré-canonicalização a exige antes de publicar. O ciclo entre classes/regras
    /// que nenhuma factory isolada pega é exatamente o que o grafo conjunto existe para recusar.
    /// </summary>
    [Fact(DisplayName = "Ciclo de derivação passa pela definição isolada mas é recusado no gate de pré-canonicalização")]
    public void CicloDeDerivacao_RecusadoNoGateDePublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();

        ConfiguracaoDerivacaoFato d1 = ConfiguracaoDerivacaoFato.Criar("D1", [Regra(0, "X", ("D2", true))]).Value!;
        ConfiguracaoDerivacaoFato d2 = ConfiguracaoDerivacaoFato.Criar("D2", [Regra(0, "Y", ("D1", true))]).Value!;

        processo.DefinirRegrasDerivacao([d1, d2]).IsSuccess.Should().BeTrue(
            "a definição isolada só barra código duplicado — o ciclo cruza duas configurações e passa aqui");

        processo.PendenciaPreCanonicalizacao().Should().NotBeNull();
        processo.PendenciaPreCanonicalizacao()!.Code
            .Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }

    [Fact(DisplayName = "MODALIDADE que contribui código fora das modalidades ofertadas é recusada no gate de pré-canonicalização")]
    public void ContribuiForaDoDominioDoProcesso_RecusadoNoGateDePublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        OfertarModalidades(processo, "AC");

        // "V" é rótulo de exibição de AC_PCD no edital, nunca código — e não está entre as
        // modalidades ofertadas por este processo. Contribuí-lo é recusado, sem tradução de alias.
        processo.DefinirRegrasDerivacao([ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [Ancora(0, "V")]).Value!])
            .IsSuccess.Should().BeTrue("a definição isolada não conhece o domínio de modalidades ofertadas");

        processo.PendenciaPreCanonicalizacao().Should().NotBeNull();
        processo.PendenciaPreCanonicalizacao()!.Code
            .Should().Be(RegrasDerivacaoFatoErrorCodes.ContribuiForaDoDominio);
    }

    [Fact(DisplayName = "MODALIDADE que contribui apenas códigos ofertados passa o gate de pré-canonicalização")]
    public void ContribuiDentroDoDominioDoProcesso_PassaNoGateDePublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();
        OfertarModalidades(processo, "AC");

        processo.DefinirRegrasDerivacao([ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [Ancora(0, "AC")]).Value!])
            .IsSuccess.Should().BeTrue();

        processo.PendenciaPreCanonicalizacao().Should().BeNull(
            "AC está entre as modalidades ofertadas — o gate de domínio de contribuição não barra");
    }

    [Fact(DisplayName = "Regra de derivação que cita fato inexistente é recusada no gate de pré-canonicalização")]
    public void DerivacaoCitaFatoInexistente_RecusadaNoGateDePublicacao()
    {
        ProcessoSeletivo processo = NovoProcesso();

        ConfiguracaoDerivacaoFato d1 = ConfiguracaoDerivacaoFato.Criar("D1", [Regra(0, "X", ("BOGUS", true))]).Value!;

        processo.DefinirRegrasDerivacao([d1]).IsSuccess.Should().BeTrue(
            "a definição isolada só valida forma e unicidade — não conhece o universo de fatos do processo");

        processo.PendenciaPreCanonicalizacao().Should().NotBeNull();
        processo.PendenciaPreCanonicalizacao()!.Code
            .Should().Be(FatoColetadoErrorCodes.PrecondicaoCitaFatoNaoColetado);
    }

    [Fact(DisplayName = "Define e reidrata a configuração de derivação em rascunho")]
    public void Definir_EmRascunho_Aceita()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirRegrasDerivacao([ConfigModalidade()]);

        resultado.IsSuccess.Should().BeTrue(resultado.Error?.Message);
        processo.RegrasDerivacao.Should().ContainSingle(c => c.CodigoFato == "MODALIDADE");
        processo.RegrasDerivacao.Single().Regras.Should().HaveCount(3);
    }

    [Fact(DisplayName = "A âncora é a regra sem condição — reconstrói uma regra incondicional")]
    public void Ancora_SemCondicao()
    {
        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", [Ancora(0, "AC")]).Value!;

        config.Regras.Single().Condicoes.Should().BeEmpty("a âncora não tem condição");

        RegrasDerivacaoFato vo = config.ParaRegrasDerivacao(DominioModalidade).Value!;
        vo.Regras.Single().EhAncora.Should().BeTrue("uma regra sem condição é a âncora incondicional");
    }

    [Fact(DisplayName = "Reconstrói o value object da regra que o motor consome, contra o domínio dado")]
    public void ParaRegrasDerivacao_ReconstroiVo()
    {
        ConfiguracaoDerivacaoFato config = ConfigModalidade();

        Result<RegrasDerivacaoFato> vo = config.ParaRegrasDerivacao(DominioModalidade);

        vo.IsSuccess.Should().BeTrue(vo.Error?.Message);
        vo.Value!.CodigoFato.Should().Be("MODALIDADE");
        vo.Value.Regras.Should().HaveCount(3);
        vo.Value.DependenciasDeclaradas.Should().BeEquivalentTo(["CONCORRER_PCD", "EGRESSO_ESCOLA_PUBLICA"],
            "as dependências são recomputadas da união dos fatos citados, nunca persistidas");
    }

    [Fact(DisplayName = "Reconstrução recusa código contribuído fora do domínio dado")]
    public void ParaRegrasDerivacao_ContribuiForaDoDominio_Falha()
    {
        ConfiguracaoDerivacaoFato config = ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
            [Ancora(0, "CODIGO_INEXISTENTE")]).Value!;

        Result<RegrasDerivacaoFato> vo = config.ParaRegrasDerivacao(DominioModalidade);

        vo.IsFailure.Should().BeTrue("um código fora do domínio congelado do processo é recusado, nunca traduzido");
    }

    [Fact(DisplayName = "Dois fatos com o mesmo código de derivação são recusados")]
    public void CodigoFatoDuplicado_Recusado()
    {
        ProcessoSeletivo processo = NovoProcesso();

        Result resultado = processo.DefinirRegrasDerivacao([ConfigModalidade(), ConfigModalidade()]);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.CodigoFatoDuplicado);
    }

    [Fact(DisplayName = "Ordem de regra duplicada na mesma configuração é recusada")]
    public void OrdemRegraDuplicada_Recusada()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("MODALIDADE",
            [Ancora(0, "AC"), Regra(0, "AC_PCD", ("CONCORRER_PCD", true))]);

        resultado.IsFailure.Should().BeTrue("a ordem das regras é total, para serialização determinística");
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.OrdemRegraDuplicada);
    }

    [Fact(DisplayName = "Configuração sem regra alguma é recusada")]
    public void SemRegras_Recusada()
    {
        Result<ConfiguracaoDerivacaoFato> resultado = ConfiguracaoDerivacaoFato.Criar("MODALIDADE", regras: []);

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(ConfiguracaoDerivacaoFatoErrorCodes.SemRegras);
    }

    [Fact(DisplayName = "Definir substitui a configuração anterior por inteiro")]
    public void Definir_SubstituiPorInteiro()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirRegrasDerivacao([ConfigModalidade()]).IsSuccess.Should().BeTrue();

        ConfiguracaoDerivacaoFato outra = ConfiguracaoDerivacaoFato.Criar("OUTRO_DERIVADO",
            [Ancora(0, "X")]).Value!;
        processo.DefinirRegrasDerivacao([outra]).IsSuccess.Should().BeTrue();

        processo.RegrasDerivacao.Should().ContainSingle(c => c.CodigoFato == "OUTRO_DERIVADO");
    }

    [Fact(DisplayName = "As regras ficam vinculadas ao processo e as condições à regra")]
    public void Vinculacao()
    {
        ProcessoSeletivo processo = NovoProcesso();

        processo.DefinirRegrasDerivacao([ConfigModalidade()]).IsSuccess.Should().BeTrue();

        ConfiguracaoDerivacaoFato config = processo.RegrasDerivacao.Single();
        config.ProcessoSeletivoId.Should().Be(processo.Id);
        config.Regras.Should().OnlyContain(r => r.ConfiguracaoDerivacaoFatoId == config.Id);
        RegraDerivacaoConfigurada comCondicao = config.Regras.Single(r => r.Contribui == "LI_PCD");
        comCondicao.Condicoes.Should().OnlyContain(c => c.RegraDerivacaoConfiguradaId == comCondicao.Id);
    }

    // ── Grafo de dependência conjunto a partir da configuração do agregado (Story #928, §6) ────────

    private static CondicaoPrecondicaoFato Precond(string fato) =>
        CondicaoPrecondicaoFato.Criar(1, fato, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!;

    private static ConfiguracaoDerivacaoFato DerivadoDe(string codigo, params string[] citados) =>
        ConfiguracaoDerivacaoFato.Criar(codigo,
        [
            RegraDerivacaoConfigurada.Criar(0, "AC",
                [.. citados.Select(static c => CondicaoRegraDerivacao.Criar(
                    1, c, Operador.Igual, JsonSerializer.SerializeToElement(true)).Value!)]).Value!,
        ]).Value!;

    [Fact(DisplayName = "Constrói o grafo conjunto acíclico da configuração do processo em rascunho")]
    public void ConstruirGrafoDependencia_ConfigAciclica_Sucesso()
    {
        ProcessoSeletivo processo = NovoProcesso();
        processo.DefinirFatosColetados(
        [
            FatoColetado.Criar("PCD", 0, null).Value!,
            FatoColetado.Criar("CONCORRER_PCD", 1, [Precond("PCD")]).Value!,
        ]).IsSuccess.Should().BeTrue();
        processo.DefinirRegrasDerivacao([DerivadoDe("MODALIDADE", "CONCORRER_PCD")]).IsSuccess.Should().BeTrue();

        Result<GrafoDependenciaConjunta> grafo = processo.ConstruirGrafoDependencia();

        grafo.IsSuccess.Should().BeTrue(grafo.Error?.Message);
        grafo.Value!.Nos.Should().Contain(n => n.Classe == ClasseNoGrafo.Fato && n.Codigo == "MODALIDADE");
    }

    [Fact(DisplayName = "Ciclo de derivação passa nas factories das componentes mas o grafo conjunto o recusa")]
    public void ConstruirGrafoDependencia_CicloDeDerivacao_Falha()
    {
        ProcessoSeletivo processo = NovoProcesso();
        // Cada ConfiguracaoDerivacaoFato é válida isoladamente (a factory não checa citação); o ciclo
        // D1↔D2 só emerge quando as arestas de derivação são consideradas juntas.
        processo.DefinirRegrasDerivacao([DerivadoDe("D1", "D2"), DerivadoDe("D2", "D1")]).IsSuccess.Should().BeTrue();

        Result<GrafoDependenciaConjunta> grafo = processo.ConstruirGrafoDependencia();

        grafo.IsFailure.Should().BeTrue();
        grafo.Error!.Code.Should().Be(GrafoDependenciaConjuntaErrorCodes.GrafoConjuntoComCiclo);
    }
}
