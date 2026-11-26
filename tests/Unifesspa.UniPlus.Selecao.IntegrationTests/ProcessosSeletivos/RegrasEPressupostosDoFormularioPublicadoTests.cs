namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Application.Abstractions.Respostas;
using Unifesspa.UniPlus.Configuracao.Contracts;
using Unifesspa.UniPlus.Kernel.Domain.CodigosDeContrato;
using Unifesspa.UniPlus.Kernel.Domain.Enums;
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
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.IntegrationTests.TestSupport;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// Regras e pressupostos do formulário público lido do banco real: as regras são as da
/// configuração publicada, filtradas para o formulário da inscrição, e os pressupostos saem com a apresentação
/// congelada na publicação, não com a do catálogo vivo.
/// </summary>
[Collection(FormularioApresentadoPersistenciaColecao.Nome)]
public sealed class RegrasEPressupostosDoFormularioPublicadoTests
{
    private readonly CenarioDeFormularioPublicado _cenario;

    public RegrasEPressupostosDoFormularioPublicadoTests(ProcessoSeletivoDbFixture fixture)
    {
        _cenario = new CenarioDeFormularioPublicado(fixture);
    }

    [Fact(DisplayName = "As regras do formulário público são as da configuração publicada, filtradas para o formulário da inscrição, sem derivado que o formulário não cita")]
    public async Task Handle_Regras_SaoAsDaConfiguracaoPublicadaFiltradas()
    {
        // O exemplo rico tem seção, termo e grupo exibidos por condição, impedimento, três derivações
        // configuradas e as duas da oferta, que nenhuma regra da inscrição cita e dois agregados sobre a composição familiar, que o
        // envelope congela e nenhuma regra do formulário cita.
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico(variante: 9, comBancasExigidasNaPublicacao: true);
        EntradaCanonicalizacao entrada = ExemploDeEnvelopeEntrada.Entrada(processo);
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(entrada);
        await _cenario.PublicarEPersistirAsync(processo, entrada.Dados, congelado, entrada.HashDocumento, ExemploDeEnvelope.Ator);

        FormularioApresentadoDoProcessoDto lido = await _cenario.LerPublicadoAsync(processo.Id);

        RecorteDoFormulario esperado = FiltroEsperado(processo, entrada);
        ConferirRegras(lido, esperado);
        ConferirConteudo(lido, esperado, processo);
    }

    private static RecorteDoFormulario FiltroEsperado(ProcessoSeletivo processo, EntradaCanonicalizacao entrada)
    {
        // A mesma montagem sobre a configuração viva, antes de passar pelo envelope: o que o front
        // interpreta é o que a configuração declarou.
        EnvelopeReidratado vivo = EnvelopeVivo(processo, entrada);
        DefinicaoAvaliavel daConfiguracao = DefinicaoAvaliavelDoProcesso.DoCongelado(vivo).Value!;
        return RecorteDoFormulario.De(
            daConfiguracao.Definicao, FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Inscricao).Valor, daConfiguracao.Ofertas);
    }

    private static EnvelopeReidratado EnvelopeVivo(ProcessoSeletivo processo, EntradaCanonicalizacao entrada) =>
        new(
            new ConfiguracaoPublicada(
            new CronogramaDeclarado(ConfiguracaoPublicadaDeTeste.Localidade, null, [.. processo.CronogramaFases]),
            new AvaliacaoDeclarada([.. processo.Etapas], [.. processo.CriteriosDesempate], processo.Classificacao!),
            new VagasDeclaradas([.. processo.DistribuicaoVagas], 1, processo.Fada is not null,
                processo.Fada, null, processo.AtendimentoEspecializado!),
            new ExigenciasDeclaradas([.. processo.DocumentosExigidos], []),
            new ColetaDeclarada(ConfiguracaoPublicadaDeTeste.CamposDaColeta([.. processo.FatosColetados], [.. processo.GruposColetados]), [.. processo.GruposColetados], [.. processo.Formularios], [.. processo.TermosExigidos],
                [], [.. processo.RegrasDerivacao], null),
            new DivulgacaoETaxaDeclaradas(null, null), null),
            entrada.Dados, entrada.HashDocumento, entrada.FusoHorario, retificacao: null, conformidade: null,
            dadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: entrada.DadosDeCatalogo.ValoresSelecionaveisCongelados, AgregadosDosGrupos: entrada.DadosDeCatalogo.AgregadosDosGrupos, ApresentacaoDosPressupostos: entrada.DadosDeCatalogo.ApresentacaoDosPressupostos));

    private static void ConferirRegras(FormularioApresentadoDoProcessoDto lido, RecorteDoFormulario esperado)
    {
        // O envelope guarda as condições na ordem canônica: a equivalência é a da avaliação, em cada
        // combinação das respostas que as regras da inscrição citam.
        foreach ((string? corRaca, string? renda, bool rural) in
            from cor in new[] { "PRETA", "BRANCA", null }
            from faixa in new[] { "ATE_1_SM", "ACIMA_1_SM", null }
            from trabalhaNoCampo in new[] { true, false }
            select (cor, faixa, trabalhaNoCampo))
        {
            EntradaAvaliacaoFormulario entradaDaAvaliacao = Respostas(corRaca, renda, rural);
            JsonSerializer.Serialize(lido.Formulario.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web).Should().Be(
                JsonSerializer.Serialize(esperado.Regras.Avaliar(entradaDaAvaliacao).Value, JsonSerializerOptions.Web),
                $"o envelope preserva cada regra, a oferta e os agregados (cor {corRaca ?? "sem resposta"}, renda {renda ?? "sem resposta"}, campo {rural})");
        }
    }

    private static void ConferirConteudo(FormularioApresentadoDoProcessoDto lido, RecorteDoFormulario esperado, ProcessoSeletivo processo)
    {
        lido.Formulario.Pressupostos.Fatos.Select(static p => p.FatoCodigo).Should().Equal(esperado.Pressupostos);
        lido.Formulario.Regras.Agregados.Should().BeEmpty("nenhuma regra da inscrição cita os agregados da composição familiar");
        lido.Formulario.Regras.Paginas.Should().Contain(e => e.Exibicao != null, "a seção de pertencimento só aparece para quem se declarou preto");
        processo.DerivacoesEfetivas().Should().HaveCount(5);
        lido.Formulario.Regras.Derivacoes.Should().BeEmpty("nenhuma regra da inscrição cita derivado, e o contrato não publica o que o formulário não usa");
    }

    [Fact(DisplayName = "O município de residência sai com o rótulo e o tipo do catálogo congelados na publicação; renomear o fato depois muda o rascunho, não o publicado")]
    public async Task Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao()
    {
        ProcessoSeletivo processo = CenarioDeFormularioPublicado.NovoProcessoComFatoDeSelecao(nameof(Handle_PressupostoDerivado_ApresentacaoCongeladaNaPublicacao));
        DefinirFatosDoMunicipio(processo);

        await PublicarComMunicipioAsync(processo);

        FatoPressuposto publicado = Municipio(await _cenario.LerPublicadoAsync(processo.Id));
        (publicado.Rotulo, publicado.TipoDeCampo).Should().Be(("Município de residência", "MUNICIPIO"));
        publicado.CalculadoDe.Should().Equal(["ENDERECO_RESIDENCIAL"]);

        FormularioApresentadoDoProcessoDto rascunho = await _cenario.LerRascunhoAsync(processo.Id, CatalogoComOMunicipio("Cidade onde mora"));

        Municipio(rascunho).Rotulo.Should().Be("Cidade onde mora", "o rascunho lê o catálogo vivo");
        Municipio(await _cenario.LerPublicadoAsync(processo.Id)).Rotulo.Should().Be("Município de residência", "o publicado lê o rótulo congelado na versão");
    }

    private async Task PublicarComMunicipioAsync(ProcessoSeletivo processo)
    {
        DefinicaoAvaliavel naPublicacao = DefinicaoAvaliavelDoProcesso.DaConfiguracaoViva(processo, CatalogoComOMunicipio("Município de residência")).Value!;
        const string hashDocumento = "3333333333333333333333333333333333333333333333333333333333333333";
        DadosEdital dados = CenarioDeFormularioPublicado.DadosDoEdital(Guid.CreateVersion7());
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(new EntradaCanonicalizacao(processo, dados, hashDocumento, FusoInstitucional.ZoneId, Retificacao: null, Conformidade: null, DadosDeCatalogo: new DadosDeCatalogoDaConfiguracaoPublicada(ValoresSelecionaveisCongelados: naPublicacao.ValoresSelecionaveis, ApresentacaoDosPressupostos: naPublicacao.ApresentacaoDosPressupostos, TiposDeAtoCongelados: TiposDeAtoCongeladosDeTeste.Dos(processo), FatosDerivadosCongelados: FatosDerivadosCongeladosDeTeste.Padrao)));
        await _cenario.PublicarEPersistirAsync(processo, dados, congelado, hashDocumento, "integration-test-user");
    }

    private static void DefinirFatosDoMunicipio(ProcessoSeletivo processo)
    {
        processo.DefinirItens(
        [
            FatoColetadoDeTeste.Coletando("COR_RACA", 0)
                .ComRotulo("Cor ou raça")
                .DoTipo(TipoDeCampo.EscolhaUnica)
                .ComObrigatoriedade(Obrigatoriedade.Sempre)
                .Construir(),
            FatoColetadoDeTeste.Coletando("ATENDIMENTO_NO_MUNICIPIO", 1)
                .ComRotulo("Deseja atendimento no município?")
                .ExibidoQuando(ExibicaoDeTeste.Quando("MUNICIPIO_RESIDENCIA", Operador.Igual, "1504208"))
                .Construir(),
        ]).IsSuccess.Should().BeTrue();
    }

    private static FaseCronograma FaseDeHabilitacaoDepoisDasDemais(ProcessoSeletivo processo) => FaseCronograma.Criar(Unifesspa.UniPlus.Selecao.Domain.ValueObjects.ChaveLocal.Criar("fase").Value!,
        OrigensCanonicasDeTeste.ComNome(new OrigemCanonicaDaFase(
            Guid.CreateVersion7(), FormularioProcesso.CodigoFaseHabilitacao, OrigemDataFase.NoEdital, AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: false,
            ColetaSolicitacaoIsencao: false)),
        new DecisoesDeCronogramaDaFase(
            processo.CronogramaFases.Max(static f => f.Ordem) + 1, Inicio: new DateTimeOffset(2026, 5, 1, 0, 0, 0, TimeSpan.Zero), Fim: new DateTimeOffset(2026, 5, 10, 18, 0, 0, TimeSpan.Zero),
            UnidadeResponsavel: UnidadesDeTeste.Ceps),
        new PublicacoesDaFase(Produtos: [ProdutoDaFase.Criar(FormularioProcesso.CodigoFaseHabilitacao, PapelDaPublicacaoPrevista.Definitivo)], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []).Value!;

    [Fact(DisplayName = "O pressuposto respondido na inscrição vem com a apresentação congelada dela; o calculado pelo sistema, com o rótulo e o tipo congelados do catálogo")]
    public async Task Handle_Habilitacao_PressupostosComAApresentacaoDaInscricao()
    {
        // A habilitação pede o certificado de quem se declarou preto na inscrição e a declaração de
        // quem é maior de idade, faixa que o sistema calcula da data de nascimento na data de
        // referência dos fatos.
        ProcessoSeletivo processo = ExemploDeEnvelopeProcessoRico.ProcessoRico(variante: 10, comBancasExigidasNaPublicacao: true);
        DefinirHabilitacao(processo);

        EntradaCanonicalizacao entrada = ExemploDeEnvelopeEntrada.Entrada(processo);
        SnapshotCanonico congelado = CenarioDeFormularioPublicado.Canonicalizar(entrada);
        await _cenario.PublicarEPersistirAsync(processo, entrada.Dados, congelado, entrada.HashDocumento, ExemploDeEnvelope.Ator);

        FormularioApresentadoDoProcessoDto lido = await _cenario.LerPublicadoAsync(processo.Id, FormulariosDoProcessoDeTeste.Habilitacao);

        ConferirPressupostosDaHabilitacao(lido);
    }

    private static void ConferirPressupostosDaHabilitacao(FormularioApresentadoDoProcessoDto lido)
    {
        lido.Formulario.Pressupostos.Fatos.Select(static p => p.FatoCodigo).Should().Equal("COR_RACA", "FAIXA_ETARIA");
        FatoPressuposto corRaca = lido.Formulario.Pressupostos.Fatos[0];
        corRaca.Rotulo.Should().Be("Cor ou raça");
        corRaca.TipoDeCampo.Should().Be("ESCOLHA_UNICA");
        corRaca.ValoresSelecionaveis!.Select(static v => v.Codigo).Should().Equal(["BRANCA", "PRETA", "PARDA"], "a simulação pergunta o dado anterior com os valores congelados");
        FatoPressuposto faixaEtaria = lido.Formulario.Pressupostos.Fatos[1];
        faixaEtaria.Rotulo.Should().Be("Faixa etária", "nenhum formulário a pergunta, e o rótulo é o do catálogo congelado na versão");
        faixaEtaria.TipoDeCampo.Should().Be("NUMERO");
        faixaEtaria.CalculadoDe.Should().Equal(["DATA_NASCIMENTO"]);
        lido.Formulario.Pressupostos.DataReferenciaFatos.Should().Be(new DateOnly(2026, 1, 31), "a data de referência da faixa etária é a congelada na publicação");
    }

    private static void DefinirHabilitacao(ProcessoSeletivo processo)
    {
        FaseCronograma habilitacao = FaseDeHabilitacaoDepoisDasDemais(processo);
        processo.DefinirCronogramaFases([.. processo.CronogramaFases, habilitacao], []).IsSuccess.Should().BeTrue();
        processo.DefinirFormulario(FormulariosDoProcessoDeTeste.Declarado(FormulariosDoProcessoDeTeste.Habilitacao, habilitacao.Id, obrigatorio: true), "Habilitação", FormulariosDoProcessoDeTeste.PaginasSemConjuntoBasico())
            .IsSuccess.Should().BeTrue();
        processo.DefinirReferenciaTemporalFatos(
            ReferenciaTemporalFatos.Criar(ReferenciaTipo.DataEspecifica, new DateOnly(2026, 1, 31), null).Value!)
            .IsSuccess.Should().BeTrue();
        DefinirItensDaHabilitacao(processo);
    }

    private static void DefinirItensDaHabilitacao(ProcessoSeletivo processo)
    {
        Result itens = processo.DefinirFatosColetados(
            FormulariosDoProcessoDeTeste.Codigo(FormulariosDoProcessoDeTeste.Habilitacao),
            [
                ItemDaHabilitacao("CERTIFICADO_EMITIDO", 0, "Certificado emitido", ExibicaoDeTeste.Quando("COR_RACA", Operador.Igual, "PRETA")),
                ItemDaHabilitacao("DECLARACAO_MAIORIDADE", 1, "Declaração de maioridade", ExibicaoDeTeste.Quando("FAIXA_ETARIA", Operador.MaiorIgual, 18)),
            ]);
        itens.IsSuccess.Should().BeTrue(itens.Error?.Message);
    }

    private static FatoColetado ItemDaHabilitacao(string fato, int ordem, string rotulo, PredicadoDnf exibicao) =>
        FatoColetadoDeTeste.Coletando(fato, ordem)
            .ComRotulo(rotulo)
            .ComObrigatoriedade(Obrigatoriedade.Sempre)
            .ExibidoQuando(exibicao)
            .NaSecao(FormularioDeTeste.Secao)
            .Construir();

    private static FatoPressuposto Municipio(FormularioApresentadoDoProcessoDto formulario) =>
        formulario.Formulario.Pressupostos.Fatos.Single(static p => p.FatoCodigo == "MUNICIPIO_RESIDENCIA");

    private static IReadOnlyList<FatoCandidatoView> CatalogoComOMunicipio(string nomeDoMunicipio) => CatalogoDoConjuntoBasico.Com(
    [
        new FatoCandidatoView(
            Guid.CreateVersion7(), "ATENDIMENTO_NO_MUNICIPIO", "Atendimento no município", "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.SimOuNao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Declarado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null,
            "INSCRICAO", "CAMPO_FORMULARIO:ATENDIMENTO_NO_MUNICIPIO", null, null, Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
        new FatoCandidatoView(
            Guid.CreateVersion7(), "MUNICIPIO_RESIDENCIA", nomeDoMunicipio, "Descrição do fato.", CodigoComRotulo<TipoDoDado>.De(TipoDoDado.Opcao), CodigoComRotulo<OrigemFato>.De(OrigemFato.Calculado), CodigoComRotulo<QuantidadeDeValores>.De(QuantidadeDeValores.ValorUnico), null,
            "INSCRICAO", "SISTEMA:MUNICIPIO_RESIDENCIA", null, CodigoComRotulo<FonteValoresFato>.De(FonteValoresFato.Municipios), Ativo: true, ClassificacaoProtecao: CodigoComRotulo<ClassificacaoDeProtecao>.De(CatalogoDe<ClassificacaoDeProtecao>.Obter("PESSOAL")), HipoteseLegal: CodigoComRotulo<HipoteseLegalTratamento>.De(CatalogoDe<HipoteseLegalTratamento>.Obter(ProtecaoDoFatoDeTeste.HipoteseLegal)), FinalidadeTratamento: ProtecaoDoFatoDeTeste.Finalidade, ProtecaoAprovada: true),
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
            Ocorrencias(trabalhaNoCampo));
    }

    private static Dictionary<string, IReadOnlyList<OcorrenciaRespondida>> Ocorrencias(bool trabalhaNoCampo) =>
        new(StringComparer.Ordinal)
        {
            ["COMPOSICAO_FAMILIAR"] =
            [
                new("membro-1", new Dictionary<string, JsonElement>(StringComparer.Ordinal)
                {
                    ["TRABALHADOR_RURAL"] = JsonSerializer.SerializeToElement(trabalhaNoCampo),
                }),
            ],
        };
}
