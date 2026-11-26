namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using System.Text.Json;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Enums;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.Events;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using static InscricaoDeTeste;

public sealed class InscricaoVersoesTests
{
    [Fact(DisplayName = "Reidratar reconstrói o estado persistido, com os valores de domínio e a revisão")]
    public void Reidratar_ReconstroiEstadoPersistido()
    {
        Guid id = Guid.CreateVersion7();

        Inscricao inscricao = Inscricao.Reidratar(id, Processo,
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Enviada, "000123", CancelamentoDaInscricao.Reidratar(null, null, null)),
            FormularioComOferta(OfertaDoProcesso), 3, new DatasDeGravacaoDaInscricao(default, null));

        inscricao.Id.Should().Be(id);
        inscricao.Estado.Situacao.Should().Be(SituacaoInscricao.Enviada);
        inscricao.Formulario.Preenchimento.Opcoes.Primeira.Should().Be(OfertaDoProcesso);
        inscricao.Estado.NumeroInscricao.Should().Be("000123");
        inscricao.Revisao.Should().Be(3);
        inscricao.ETag.Should().Be($"\"{id}:3:{VersaoFormulario}\"");
        inscricao.Formulario.Preenchimento.Respostas.Should().ContainKey("NOME_SOCIAL");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Reidratar_DatasGravadas_PreservaPrecisao(bool temAtualizacao)
    {
        DateTimeOffset criada = DateTimeOffset.Parse("2025-01-02T03:04:05.123456+00:00", System.Globalization.CultureInfo.InvariantCulture);
        DateTimeOffset? atualizada = temAtualizacao ? criada.AddMinutes(1) : null;
        Inscricao inscricao = Inscricao.Reidratar(Guid.NewGuid(), Processo,
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Inicial, FormularioComOferta(OfertaDoProcesso), 1,
            new DatasDeGravacaoDaInscricao(criada, atualizada));

        inscricao.CreatedAt.Should().Be(criada);
        inscricao.UpdatedAt.Should().Be(atualizada);
    }

    [Fact]
    public void Reidratar_CancelamentoSemInstante_PreservaContaEMotivo()
    {
        Inscricao inscricao = Inscricao.Reidratar(Guid.NewGuid(), Processo,
            TitularDaInscricao.Reidratar(PersonasDeTeste.FernandaDrumond, "conta-1", "https://issuer"),
            EstadoDaInscricao.Reidratar(SituacaoInscricao.Cancelada, "000123", CancelamentoDaInscricao.Reidratar(null, "conta-1", "motivo")),
            FormularioDaInscricao.Reidratar(VersaoFormulario, PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(null, null), Respostas(), SemGrupos(), SemAceites()),
                SemDerivados(), DefinicoesDasRespostas), 3, new Unifesspa.UniPlus.Selecao.Domain.ValueObjects.DatasDeGravacaoDaInscricao(default, null));

        inscricao.UpdatedAt.Should().BeNull();
        inscricao.Estado.Cancelamento!.CanceladaEm.Should().BeNull();
        inscricao.Estado.Cancelamento.CanceladaPor.Should().Be("conta-1");
        inscricao.Estado.Cancelamento.MotivoDoCancelamento.Should().Be("motivo");
    }

    [Fact(DisplayName = "Adotar a versão em que o rascunho já está não muda nada, nem a revisão")]
    public void AdotarVersao_NaMesmaVersao_NaoMudaNada()
    {
        Inscricao inscricao = NovaInscricao();
        int revisao = inscricao.Revisao;

        Result resultado = inscricao.AdotarVersao(VersaoFormulario, Admitidas with { DoNivelSuperior = new HashSet<string>(StringComparer.Ordinal) });

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Revisao.Should().Be(revisao);
    }

    [Fact(DisplayName = "A inscrição enviada não troca de versão: a adoção é recusada e a versão congelada fica")]
    public void AdotarVersao_NaInscricaoEnviada_Recusa()
    {
        Inscricao inscricao = NovaInscricao();
        SubmeterCom(inscricao, FormularioDeTeste.AvaliacaoSemOcultos).IsSuccess.Should().BeTrue();

        Result resultado = inscricao.AdotarVersao(VersaoRetificada, Admitidas);

        resultado.Error!.Code.Should().Be(InscricaoErrorCodes.RascunhoSoAntesDoEnvio);
        inscricao.Formulario.VersaoFormularioId.Should().Be(VersaoFormulario);
    }

    [Fact(DisplayName = "O ETag muda com a versão do formulário, para o If-Match de uma versão não valer na adoção de outra")]
    public void ETag_DependeDaVersaoDoFormulario()
    {
        Inscricao deUma = NovaInscricao();
        Inscricao deOutra = NovaInscricao();
        deOutra.AdotarVersao(VersaoRetificada, Admitidas).IsSuccess.Should().BeTrue();
        deUma.AdotarVersao(Guid.CreateVersion7(), Admitidas).IsSuccess.Should().BeTrue();

        deUma.Revisao.Should().Be(deOutra.Revisao);
        deUma.ETag.Should().NotBe(deOutra.ETag.Replace(deOutra.Id.ToString(), deUma.Id.ToString(), StringComparison.Ordinal));
    }

    [Fact(DisplayName = "O rascunho que adota a versão nova perde, sem recusa, a resposta, o grupo e o aceite que ela não admite, e a revisão sobe")]
    public void AdotarVersao_RetiraORespostaOGrupoEOAceiteQueAVersaoNaoAdmite()
    {
        Inscricao inscricao = ComDadosDaVersaoAnterior();
        int revisao = inscricao.Revisao;

        RespostasAdmitidas daVersaoNova = AdmitidasDaVersaoRetificada();

        Result resultado = inscricao.AdotarVersao(VersaoRetificada, daVersaoNova);

        resultado.IsSuccess.Should().BeTrue();
        inscricao.Formulario.VersaoFormularioId.Should().Be(VersaoRetificada);
        inscricao.Formulario.Preenchimento.Respostas.Keys.Should().Equal("NOME_SOCIAL");
        inscricao.Formulario.Preenchimento.Grupos.Keys.Should().Equal("COMPOSICAO_FAMILIAR");
        inscricao.Formulario.Preenchimento.Grupos["COMPOSICAO_FAMILIAR"].Single().Respostas.Keys.Should().Equal("PARENTESCO");
        inscricao.Formulario.Preenchimento.Aceites.Select(static a => a.Codigo).Should().Equal(TermoVeracidade);
        inscricao.Revisao.Should().Be(revisao + 1);
    }


    private static Inscricao ComDadosDaVersaoAnterior()
    {
        Inscricao inscricao = NovaInscricao();
        inscricao.Salvar(PreenchimentoDoCandidato.ParaValidacao(OpcoesDeCursoDaInscricao.Reidratar(null, null), RespostasDaVersaoAnterior(), GruposDaVersaoAnterior(), [new AceiteDeTermo(TermoLgpd, VersaoDoTermoLgpd, HashDoTermoLgpd), new AceiteDeTermo(TermoVeracidade, VersaoDoTermoVeracidade, HashDoTermoVeracidade)]), Admitidas, PrecondicaoIfMatch.DeTags([inscricao.ETag])).IsSuccess.Should().BeTrue();
        return inscricao;
    }

    private static Dictionary<string, JsonElement> RespostasDaVersaoAnterior() =>
            new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["NOME_SOCIAL"] = JsonSerializer.SerializeToElement("Ariel Drumond"),
                ["RG_NUMERO"] = JsonSerializer.SerializeToElement("1234567"),
            };

    private static Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>> GruposDaVersaoAnterior() =>
            new Dictionary<string, IReadOnlyList<OcorrenciaDeGrupoInscricao>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = [new OcorrenciaDeGrupoInscricao("m1", Respostas(("PARENTESCO", "MAE"), ("RG_NUMERO", "7654321")))],
                ["FONTES_DE_RENDA"] = [new OcorrenciaDeGrupoInscricao("r1", Respostas(("RENDA_MENSAL", "1000")))],
            };

    private static RespostasAdmitidas AdmitidasDaVersaoRetificada() =>
        Admitidas with
        {
            DoNivelSuperior = new HashSet<string>(StringComparer.Ordinal) { "NOME_SOCIAL" },
            SubitensPorGrupo = new Dictionary<string, IReadOnlySet<string>>(StringComparer.Ordinal)
            {
                ["COMPOSICAO_FAMILIAR"] = new HashSet<string>(StringComparer.Ordinal) { "PARENTESCO" },
            },
            Termos = new Dictionary<string, TermoAdmitido>(StringComparer.Ordinal)
            {
                [TermoLgpd] = new(VersaoDoTermoLgpd, "hash-lgpd-v3"),
                [TermoVeracidade] = new(VersaoDoTermoVeracidade, HashDoTermoVeracidade),
            },
        };


    private static FormularioDaInscricao FormularioComOferta(Guid oferta) =>
        FormularioDaInscricao.Reidratar(VersaoFormulario,
            PreenchimentoDoCandidato.Criar(OpcoesDeCursoDaInscricao.Reidratar(oferta, null), Respostas(), SemGrupos(), SemAceites()),
            SemDerivados(), DefinicoesDasRespostas);
}
