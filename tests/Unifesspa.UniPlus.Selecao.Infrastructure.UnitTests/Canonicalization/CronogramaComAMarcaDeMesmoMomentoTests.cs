namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Canonicalization;

using System.Text.Json.Nodes;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Selecao.Infrastructure.Canonicalization;

/// <summary>
/// A marca "no mesmo momento da anterior" na fase do cronograma do envelope: a chave existe em toda fase, a marca é restaurada
/// fielmente, muda os bytes e o hash, e a gramática fechada da fase recusa a chave ausente e o valor que não é booleano.
/// </summary>
public sealed class CronogramaComAMarcaDeMesmoMomentoTests
{
    private const string Chave = "noMesmoMomentoDaAnterior";

    private static FaseCronograma Fase(bool marcada) => FaseCronograma.Reidratar(
        new Guid("6666aaaa-0000-4000-8000-000000000002"),
        new OrigemCanonicaDaFase(
            OrigemId: new Guid("4444dddd-0000-4000-8000-000000000002"), Codigo: "INSCRICAO", OrigemData: OrigemDataFase.NoEdital,
            AgrupaEtapas: false, PermiteComplementacao: false, ColetaInscricao: true, ColetaSolicitacaoIsencao: false),
        new DecisoesDeCronogramaDaFase(
            Ordem: 2, Inicio: null, Fim: null, UnidadeResponsavel: new UnidadeResponsavelSnapshot(new Guid("5555eeee-0000-4000-8000-000000000002"), "CEPS", "Centro de Processos Seletivos"))
        { NoMesmoMomentoDaAnterior = marcada },
        new PublicacoesDaFase(Produtos: [], EmiteParecerIndividual: false, RegraRecurso: null),
        bancasRequeridas: []);

    private static byte[] Bytes(FaseCronograma fase) => PerfilCanonicoV1.Instancia.Serializar(SerializacaoDoCronograma.SerializarFase(fase));

    private static JsonObject SacarFase(FaseCronograma fase) => JsonNode.Parse(Bytes(fase))!.AsObject();

    private static LeitorEnvelope Ler(JsonObject item, out IReadOnlyList<FaseCronograma> fases)
    {
        LeitorEnvelope leitor = new();
        fases = LeituraDoCronograma.LerCronogramaFases(leitor, Payload(item));

        return leitor;
    }

    private static JsonObject Payload(JsonObject item) => new()
    {
        ["cronogramaFases"] = new JsonObject
        {
            ["origemCandidatos"] = OrigemCandidatos.InscricaoPropria.ToString(),
            ["fases"] = new JsonArray(item),
        },
    };

    [Theory(DisplayName = "A fase grava a chave da marca, marcada ou não, e a restaura fielmente")]
    [InlineData(true)]
    [InlineData(false)]
    public void Fase_GravaERestauraAMarca(bool marcada)
    {
        JsonObject item = SacarFase(Fase(marcada));

        LeitorEnvelope leitor = Ler(item, out IReadOnlyList<FaseCronograma> fases);

        item[Chave]!.GetValue<bool>().Should().Be(marcada);
        leitor.Falhou.Should().BeFalse();
        fases.Single().NoMesmoMomentoDaAnterior.Should().Be(marcada);
        fases.Single().Ordem.Should().Be(2);
    }

    [Fact(DisplayName = "A marca muda os bytes canônicos e o hash do envelope, e a mesma marca produz os mesmos bytes")]
    public void Marca_MudaBytesEHash()
    {
        byte[] marcada = Bytes(Fase(true));
        byte[] naoMarcada = Bytes(Fase(false));

        naoMarcada.Should().NotEqual(marcada);
        HashCanonicalComputer.ComputeSha256Hex(naoMarcada).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(marcada));
        Bytes(Fase(true)).Should().Equal(marcada);
    }

    [Fact(DisplayName = "A fase sem a chave da marca é recusada como envelope malformado: a escrita sempre a grava")]
    public void ChaveAusente_Recusada()
    {
        JsonObject item = SacarFase(Fase(true));
        item.Remove(Chave);

        LeitorEnvelope leitor = Ler(item, out _);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
        leitor.Erro.Message.Should().Contain(Chave);
    }

    [Fact(DisplayName = "A marca que não é booleana é recusada como envelope malformado")]
    public void MarcaQueNaoEBooleana_Recusada()
    {
        JsonObject item = SacarFase(Fase(true));
        item[Chave] = "sim";

        LeitorEnvelope leitor = Ler(item, out _);

        leitor.Falhou.Should().BeTrue();
        leitor.Erro.Code.Should().Be(ErrosCodecEnvelope.EnvelopeMalformado);
    }
}
