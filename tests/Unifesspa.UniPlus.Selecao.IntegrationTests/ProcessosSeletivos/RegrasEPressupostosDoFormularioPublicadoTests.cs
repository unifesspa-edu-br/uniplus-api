namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Kernel.Tempo;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Services;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Regras e pressupostos do formulário público lido do banco real: as regras são as da
/// configuração publicada, recortadas para a finalidade, e os pressupostos saem com a apresentação
/// congelada na publicação, não com a do catálogo vivo.
/// </summary>
[Collection(FormularioRenderizavelPersistenciaColecao.Nome)]
public sealed class RegrasEPressupostosDoFormularioPublicadoTests
{
    private readonly CenarioDeFormularioPublicado _cenario;

    public RegrasEPressupostosDoFormularioPublicadoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeFormularioPublicado(fixture);
    }

    [Fact(DisplayName = "As regras do formulário público são as da configuração publicada, recortadas para a finalidade, sem derivado que o formulário não cita")]
    public async Task Handle_Regras_SaoAsDaConfiguracaoPublicadaRecortadas()
    {
        // O corpus rico tem seção, termo e grupo exibidos por condição, impedimento, três derivações
        // configuradas e as duas da oferta, que nenhuma regra da inscrição cita e dois agregados sobre a composição familiar, que o
        // envelope congela e nenhuma regra do formulário cita.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 9);
        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo);
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(entrada);
        await _cenario.PublicarEPersistirAsync(processo, entrada.Dados, congelado, entrada.HashDocumento, CorpusEnvelope.Ator);

        FormularioRenderizavelDto lido = await _cenario.LerPublicadoAsync(processo.Id);

        // A mesma montagem sobre a configuração viva, antes de passar pelo envelope: o que o front
        // interpreta é o que a configuração declarou.
        EnvelopeReidratado vivo = new(
            new GrafoConfiguracao(
                [.. processo.Etapas], processo.AtendimentoEspecializado!, [.. processo.DistribuicaoVagas], processo.BonusRegional,
                [.. processo.CriteriosDesempate], processo.Classificacao!, [.. processo.CronogramaFases], [.. processo.DocumentosExigidos], [], null,
                fatosColetados: [.. processo.FatosColetados], regrasDerivacao: [.. processo.RegrasDerivacao], formularios: [.. processo.Formularios],
                termosExigidos: [.. processo.TermosExigidos], gruposColetados: [.. processo.GruposColetados],
            numeroDeOpcoesDeCurso: 1),
            entrada.Dados, entrada.HashDocumento, entrada.FusoHorario, retificacao: null, conformidade: null,
            valoresSelecionaveisCongelados: entrada.ValoresSelecionaveisCongelados, agregadosDosGrupos: entrada.AgregadosDosGrupos, apresentacaoDosPressupostos: entrada.ApresentacaoDosPressupostos);
        DefinicaoAvaliavel daConfiguracao = DefinicaoAvaliavelDoProcesso.DoCongelado(vivo).Value!;
        RecorteDaFinalidade esperado = RecorteDaFinalidade.De(
            daConfiguracao.Definicao, DefinicaoDoProcesso.CodigoDaEtapa(FinalidadeFormulario.Inscricao, null), daConfiguracao.Ofertas);

        // O envelope guarda as condições na ordem canônica: a equivalência é a da avaliação, em cada
        // combinação das respostas que as regras da inscrição citam.
        foreach ((string? corRaca, string? renda, bool rural) in
            from cor in new[] { "PRETA", "BRANCA", null }
            from faixa in new[] { "ATE_1_SM", "ACIMA_1_SM", null }
            from trabalhaNoCampo in new[] { true, false }
            select (cor, faixa, trabalhaNoCampo))
        {
            EntradaAvaliacaoFormulario entradaDaAvaliacao = Respostas(corRaca, renda, rural);
            JsonSerializer.Serialize(lido.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web).Should().Be(
                JsonSerializer.Serialize(esperado.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web),
                $"o envelope preserva cada regra, a oferta e os agregados (cor {corRaca ?? "sem resposta"}, renda {renda ?? "sem resposta"}, campo {rural})");
        }

        lido.Pressupostos.Select(static p => p.FatoCodigo).Should().Equal(esperado.Pressupostos);
        lido.Regras.Agregados.Should().BeEmpty("nenhuma regra da inscrição cita os agregados da composição familiar");
        lido.Regras.Etapas.Should().Contain(e => e.Exibicao != null, "a seção de pertencimento só aparece para quem se declarou preto");
        processo.DerivacoesEfetivas().Should().HaveCount(5);
        lido.Regras.Derivacoes.Should().BeEmpty("nenhuma regra da inscrição cita derivado, e o contrato não publica o que o formulário não usa");
    }

    [Fact(DisplayName = "O município de residência sai com o rótulo e o tipo do catálogo congelados na publicação; renomear o fato depois muda o rascunho, não o publicado")]
    public async Task Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao()
    {
        ProcessoSeletivo processo = CenarioDeFormularioPublicado.NovoProcessoComFatoDeSelecao(nameof(Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao));
        processo.DefinirItens(
        [
            FatoColetado.Criar("COR_RACA", 0, "Cor ou raça", TipoRenderizacao.SelecaoUnica, Obrigatoriedade.Sempre, null, classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("ATENDIMENTO_NO_MUNICIPIO", 1, "Deseja atendimento no município?", TipoRenderizacao.Booleano, Obrigatoriedade.Nunca, [
                CondicaoPrecondicaoFato.Criar(0, "MUNICIPIO_RESIDENCIA", Operador.Igual, JsonSerializer.SerializeToElement("1504208")).Value!,
            ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();

        DefinicaoAvaliavel naPublicacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, CatalogoComOMunicipio("Município de residência")).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        DadosEdital dados = CenarioDeFormularioPublicado.DadosDoEdital(Guid.CreateVersion7());
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(new EntradaCanonicalizacao(
            processo, dados, hashDocumento, FusoInstitucional.ZoneId,
            ValoresSelecionaveisCongelados: naPublicacao.ValoresSelecionaveis,
            ApresentacaoDosPressupostos: naPublicacao.ApresentacaoDosPressupostos,
            TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao));
        await _cenario.PublicarEPersistirAsync(processo, dados, congelado, hashDocumento, "integration-test-user");

        PressupostoRenderizavel publicado = Municipio(await _cenario.LerPublicadoAsync(processo.Id));
        (publicado.Rotulo, publicado.TipoRenderizacao).Should().Be(("Município de residência", "MUNICIPIO"));
        publicado.CalculadoDe.Should().Equal(["ENDERECO_RESIDENCIAL"]);

        FormularioRenderizavelDto rascunho = await _cenario.LerRascunhoAsync(processo.Id, CatalogoComOMunicipio("Cidade onde mora"));

        Municipio(rascunho).Rotulo.Should().Be("Cidade onde mora", "o rascunho lê o catálogo vivo");
        Municipio(await _cenario.LerPublicadoAsync(processo.Id)).Rotulo.Should().Be("Município de residência", "o publicado lê o rótulo congelado na versão");
    }

    private static FaseCronograma FaseDeHabilitacaoDepoisDasDemais(ProcessoSeletivo processo) => FaseCronograma.Criar(
        new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            processo.CronogramaFases.Max(static f => f.Ordem) + 1, Inicio: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
            FaseConcluinteCodigo: null, UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelProdutoFase.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "O pressuposto respondido na inscrição vem com a apresentação congelada dela; o calculado pelo sistema, com o rótulo e o tipo congelados do catálogo")]
    public async Task Handle_Habilitacao_PressupostosComAApresentacaoDaInscricao()
    {
        // A habilitação pede o certificado de quem se declarou preto na inscrição e a declaração de
        // quem é maior de idade, faixa que o sistema calcula da data de nascimento na data de
        // referência dos fatos.
        ProcessoSeletivo processo = CorpusEnvelope.ProcessoRico(variante: 10);
        FaseCronograma habilitacao = FaseDeHabilitacaoDepoisDasDemais(processo);
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], [], PrecondicaoIfMatch.Ausente).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FinalidadeFormulario.Habilitacao, habilitacao.Id, "Habilitação", FormularioDeTeste.Etapas(), PrecondicaoIfMatch.Ausente)
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!, PrecondicaoIfMatch.Curinga)
            .IsSuccess.Should().BeTrue();
        Result itens = processo.DefinirItens(
        [
            FatoColetado.Criar("CERTIFICADO_EMITIDO", 0, "Certificado emitido", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "COR_RACA", Operador.Igual, JsonSerializer.SerializeToElement("PRETA")).Value!,
            ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
            FatoColetado.Criar("DECLARACAO_MAIORIDADE", 1, "Declaração de maioridade", TipoRenderizacao.Booleano, Obrigatoriedade.Sempre, [
                CondicaoPrecondicaoFato.Criar(0, "FAIXA_ETARIA", Operador.MaiorIgual, JsonSerializer.SerializeToElement(18)).Value!,
            ], classificacaoProtecao: "PESSOAL", hipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, finalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, descricao: ProtecaoDoFatoDeTeste.Descricao).Value!,
        ], finalidade: FinalidadeFormulario.Habilitacao);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);

        EntradaCanonicalizacao entrada = CorpusEnvelope.Entrada(processo);
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(entrada);
        await _cenario.PublicarEPersistirAsync(processo, entrada.Dados, congelado, entrada.HashDocumento, CorpusEnvelope.Ator);

        FormularioRenderizavelDto lido = await _cenario.LerPublicadoAsync(processo.Id, FinalidadeFormulario.Habilitacao);

        lido.Pressupostos.Select(static p => p.FatoCodigo).Should().Equal("COR_RACA", "FAIXA_ETARIA");
        PressupostoRenderizavel corRaca = lido.Pressupostos[0];
        corRaca.Rotulo.Should().Be("Cor ou raça");
        corRaca.TipoRenderizacao.Should().Be("SELECAO_UNICA");
        corRaca.ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal(["BRANCA", "PRETA", "PARDA"], "a simulação pergunta o dado anterior com os valores congelados");
        PressupostoRenderizavel faixaEtaria = lido.Pressupostos[1];
        faixaEtaria.Rotulo.Should().Be("Faixa etária", "nenhum formulário a pergunta, e o rótulo é o do catálogo congelado na versão");
        faixaEtaria.TipoRenderizacao.Should().Be("NUMERO");
        faixaEtaria.CalculadoDe.Should().Equal(["DATA_NASCIMENTO"]);
        lido.DataReferenciaFatos.Should().Be(new DateOnly(2026, 1, 31), "a âncora da faixa etária é a congelada na publicação");
    }

    private static PressupostoRenderizavel Municipio(FormularioRenderizavelDto formulario) =>
        formulario.Pressupostos.Single(static p => p.FatoCodigo == "MUNICIPIO_RESIDENCIA");

    private static IReadOnlyList<FatoCandidatoView> CatalogoComOMunicipio(string nomeDoMunicipio) => CatalogoDoConjuntoBasico.Com(
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "ATENDIMENTO_NO_MUNICIPIO", "Atendimento no município", "Descrição do fato.", "BOOLEANO", "DECLARADO", "ESCALAR", null,
            "INSCRICAO", "CAMPO_FORMULARIO:ATENDIMENTO_NO_MUNICIPIO", null, null, Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new FatoCandidatoView(
            Guid.CreateVersion7(), "MUNICIPIO_RESIDENCIA", nomeDoMunicipio, "Descrição do fato.", "CATEGORICO", "DERIVADO", "ESCALAR", null,
            "INSCRICAO", "SISTEMA:MUNICIPIO_RESIDENCIA", null, "GEO_MUNICIPIO", Ativo: true, ClassificacaoProtecao: "PESSOAL", HipoteseLegal: ProtecaoDoFatoDeTeste.HipoteseLegal, FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
    ]);

    private static EntradaAvaliacaoFormulario Respostas(string? corRaca, string? renda, bool trabalhaNoCampo)
    {
        Dictionary<string, JsonElement> respostas = new(StringComparer.Ordinal);
        if (corRaca is not null)
        {
            respostas["COR_RACA"] = JsonSerializer.SerializeToElement(corRaca);
        }

        if (renda is not null)
        {
            respostas["RENDA"] = JsonSerializer.SerializeToElement(renda);
        }

        return new EntradaAvaliacaoFormulario(
            respostas, new HashSet<string>(StringComparer.Ordinal), new Dictionary<string, FatoResolvido>(StringComparer.Ordinal), Envio: null,
            new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] =
                [
                    new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                    {
                        ["TRABALHADOR_RURAL"] = JsonSerializer.SerializeToElement(trabalhaNoCampo),
                    }),
                ],
            });
    }
}
