namespace Unifesspa.UniPlus.Selecao.Application.UnitTests.Commands;

using System.Text.Json;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Authentication;
using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Application.Commands.Inscricoes;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.DTOs.Comprovantes;
using Unifesspa.UniPlus.Selecao.Application.Services;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static FormularioDoEnvioDeTeste;
using static SubmeterDeTeste;

/// <summary>
/// A consulta feita depois do envio usa a residência gravada, sem voltar ao geo.
/// </summary>
public sealed class SubmeterInscricaoConsultaDepoisDoEnvioTests
{
    [Fact(DisplayName = "Regra sobre o município do grupo mantém o veredito depois do envio")]
    public async Task Consulta_RegraSobreMunicipioDeGrupo_TemOMesmoVeredito()
    {
        Cenario cenario = new(MontarVersaoComItemObrigatorioETermo(grupos: [Locais()]));
        InscricaoRequest pedido = cenario.Pedido with
        {
            Grupos = new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricaoInput>>(StringComparer.Ordinal)
            {
                ["LOCAIS"] = [new("local-1", Texto(("LOCAL_MUNICIPIO", Belem), ("OBSERVACAO_DE_BELEM", "Perto do centro")))],
            },
        };
        await cenario.EnviarAsync(pedido);
        cenario.Inscricao.Formulario.Preenchimento.Grupos["LOCAIS"].Single().Respostas.Should().ContainKey("OBSERVACAO_DE_BELEM");
        ResultadoDaAvaliacaoDaInscricao consulta = await cenario.ConsultarAsync();
        consulta.Formulario.Grupos.Single().Ocorrencias.Single().Itens.Single(static i => i.FatoCodigo == "OBSERVACAO_DE_BELEM")
            .Visivel.Should().Be(Ternario.Verdadeiro);
    }

    [Fact(DisplayName = "Consulta após o envio mantém a residência gravada mesmo com o Geo indisponível")]
    public async Task Consulta_DepoisDoEnvio_UsaAResidenciaGravadaSemOGeo()
    {
        Cenario cenario = new(MontarVersaoComItemObrigatorioETermo(exigencias: ExigenciaRegional));
        await cenario.EnviarAsync(cenario.Pedido);
        ResultadoDaAvaliacaoDaInscricao consulta = await cenario.ConsultarAsync();
        cenario.Inscricao.Formulario.Derivados["MUNICIPIO_RESIDENCIA"].GetRawText().Should().Be(ValorDeMunicipio.Gravado(Belem, "Belém").GetRawText());
        consulta.Documentos.Itens.Single().Situacao.Should().Be(SituacaoDoDocumento.Pendente);
    }

    [Fact(DisplayName = "Consulta com outra data de nascimento recalcula a idade e mantém a residência do envio")]
    public async Task Consulta_DataDeNascimentoMudada_RecalculaAFaixaEtariaESegueComAResidenciaGravada()
    {
        Cenario cenario = new(MontarVersaoComItemObrigatorioETermo(exigencias: ExigenciasDeIdadeEResidencia));
        await cenario.EnviarAsync(cenario.Pedido);
        ResultadoDaAvaliacaoDaInscricao consulta = await cenario.ConsultarAsync(Texto(("DATA_NASCIMENTO", "2010-01-01")));
        Dictionary<string, SituacaoDoDocumento> situacoes = consulta.Documentos.Itens.ToDictionary(static d => d.Documento.TipoDocumentoCodigo, static d => d.Situacao);
        situacoes["AOS16"].Should().Be(SituacaoDoDocumento.Pendente, "a faixa etária vem da data de nascimento do corpo");
        situacoes["AOS26"].Should().NotBe(SituacaoDoDocumento.Pendente);
        situacoes["DEBELEM"].Should().Be(SituacaoDoDocumento.Pendente, "a residência vem da gravada no envio");
    }

    private static GrupoColetado Locais()
    {
        FatoColetado observacao = FatoColetadoDeTeste.Coletando("OBSERVACAO_DE_BELEM", 1)
            .ComRotulo("Observação")
            .DeTexto("LIVRE")
            .ExibidoQuando(ExibicaoDeTeste.Quando("LOCAL_MUNICIPIO", Operador.Igual, Belem))
            .Construir();
        return GrupoColetadoDeTeste.Agrupando("LOCAIS", 1)
            .ComRotulo("Locais")
            .ComOcorrencias(0, null)
            .ComCampos(GrupoMunicipal().Subitens.First(), observacao)
            .DaFinalidade(FinalidadeFormulario.Inscricao)
            .Construir();
    }

    private static DocumentoExigido DocumentoCondicional(Guid fase, string codigo, string fato, JsonElement valor) =>
        new Unifesspa.UniPlus.Selecao.Domain.UnitTests.Cenarios.DocumentoExigidoBuilder()
            .ComTipo(new CopiaDoTipoDeDocumento(Guid.CreateVersion7(), codigo, codigo, "CAT", ProtecaoDoDocumentoDeTeste.Pessoal))
            .ComMomento(new MomentoDaExigencia(fase, null, FinalidadeFormulario.Inscricao))
            .ComAplicabilidade(new AplicabilidadeDaExigencia(Aplicabilidade.Condicional, [CondicaoGatilho.Criar(0, fato, Operador.Igual, valor).Value!]))
            .ComIndeferimento(new IndeferimentoDoDocumento(false, null, []))
            .ComRequisitos(new RequisitosDoDocumento(FormatosPermitidos.Criar(true, null).Value!, null, null, null, null))
            .ComFatos(null)
            .Criar().Value!;

    private static (IReadOnlyList<DocumentoExigido>, IReadOnlyList<RegraDeComprovacao>) ExigenciaRegional(ProcessoSeletivo processo)
    {
        DocumentoExigido regional = DocumentoCondicional(processo.CronogramaFases.First(static f => f.ColetaInscricao).Id,
            "REGIONAL", "MUNICIPIO_RESIDENCIA", JsonSerializer.SerializeToElement(Belem));
        return ([regional], [RegraDeComprovacao.CriarRegraDeDocumento(regional, 0).Value!]);
    }

    private static (IReadOnlyList<DocumentoExigido>, IReadOnlyList<RegraDeComprovacao>) ExigenciasDeIdadeEResidencia(ProcessoSeletivo processo)
    {
        Guid fase = processo.CronogramaFases.First(static f => f.ColetaInscricao).Id;
        DocumentoExigido aos26 = DocumentoCondicional(fase, "AOS26", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(26));
        DocumentoExigido aos16 = DocumentoCondicional(fase, "AOS16", "FAIXA_ETARIA", JsonSerializer.SerializeToElement(16));
        DocumentoExigido deBelem = DocumentoCondicional(fase, "DEBELEM", "MUNICIPIO_RESIDENCIA", JsonSerializer.SerializeToElement(Belem));
        return ([aos26, aos16, deBelem], [RegraDeComprovacao.CriarRegraDeDocumento(aos26, 0).Value!, RegraDeComprovacao.CriarRegraDeDocumento(aos16, 1).Value!, RegraDeComprovacao.CriarRegraDeDocumento(deBelem, 2).Value!]);
    }

    private sealed class Cenario
    {
        private readonly IInscricaoRepository _inscricoes = Substitute.For<IInscricaoRepository>();
        private readonly IProcessoSeletivoRepository _processos = Substitute.For<IProcessoSeletivoRepository>();
        private readonly IRegistroCodecsEnvelope _codecs = Substitute.For<IRegistroCodecsEnvelope>();

        public Cenario((VersaoConfiguracao Versao, ConfiguracaoPublicada Configuracao, VersaoTermoEscolhida Termo) configuracao)
        {
            Cpf cpf = PersonasDeTeste.FernandaDrumond;
            Inscricao = NovaInscricao(cpf, "conta-1", configuracao.Versao.Id);
            Pedido = RequestComApelidoETermoAceito(cpf, configuracao.Termo, configuracao.Configuracao);
            _inscricoes.ObterAtivaPorProcessoECpfAsync(ProcessoId, cpf, Arg.Any<CancellationToken>()).Returns(Inscricao);
            DivulgacaoDeTeste.ConfigurarVersao(_processos, _codecs, configuracao.Versao, configuracao.Configuracao);
        }

        public Inscricao Inscricao { get; }
        public InscricaoRequest Pedido { get; }

        public async Task EnviarAsync(InscricaoRequest pedido)
        {
            Result<ReciboDaInscricaoDto> enviada = await SubmeterAsync(
                new SubmeterInscricaoCommand(ProcessoId, pedido, PrecondicaoIfMatch.DeTags([Inscricao.ETag])),
                _inscricoes, _processos, _codecs, Substitute.For<IUnitOfWork>(), UsuarioComCpf(PersonasDeTeste.FernandaDrumond), GeoQueConhece());
            enviada.IsSuccess.Should().BeTrue(enviada.Error?.Message);
        }

        public async Task<ResultadoDaAvaliacaoDaInscricao> ConsultarAsync(IReadOnlyDictionary<string, JsonElement>? respostas = null)
        {
            Result<ContextoDaVersaoFixada> contexto = await new ContextoDaVersaoFixadaResolver(_processos, _codecs).ResolverAsync(
                Inscricao.Formulario.VersaoFormularioId, ProcessoId, Inscricao.Formulario.Preenchimento.Opcoes.Primeira,
                Inscricao.Formulario.Preenchimento.Opcoes.Segunda, CancellationToken.None);
            ArquivosDaInscricao arquivos = await new DocumentosEnviadosDaInscricao(Substitute.For<IDocumentoDaInscricaoRepository>()).CarregarAsync(Inscricao.Id, CancellationToken.None);
            Result<ResultadoDaAvaliacaoDaInscricao> consulta = await new PreparacaoDaAvaliacaoDaInscricao(GeoQueConhece().Indisponivel()).AvaliarAsync(
                Inscricao, contexto.Value!, respostas ?? new Dictionary<string, JsonElement>(StringComparer.Ordinal),
                new Dictionary<string, IReadOnlyList<OcorrenciaRespondida>>(StringComparer.Ordinal),
                new EnvioDasRespostas(DivulgacaoDeTeste.Relogio.GetUtcNow(), TimeZoneInfo.Utc), arquivos, CancellationToken.None);
            consulta.IsSuccess.Should().BeTrue(consulta.Error?.Message);
            return consulta.Value!;
        }
    }
}
