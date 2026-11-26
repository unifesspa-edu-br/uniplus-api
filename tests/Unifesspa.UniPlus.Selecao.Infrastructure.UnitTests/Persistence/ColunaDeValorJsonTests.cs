namespace Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.Persistence;

using System.Text.Json;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

using Unifesspa.UniPlus.Selecao.Infrastructure.Persistence.Configurations;
using Unifesspa.UniPlus.Selecao.Infrastructure.UnitTests.TestSupport;

public sealed class ColunaDeValorJsonTests
{
    private const string NomeDoValor = "valor_de_teste";
    private const string Marcador = "valor-gravado-ficticio-4c1d";

    private static readonly IProperty Propriedade = MontarModelo().FindEntityType(typeof(EntidadeComValorJson))!.FindProperty(nameof(EntidadeComValorJson.Valor))!;

    [Fact(DisplayName = "A coluna é obrigatória e do tipo jsonb")]
    public void Mapeamento_DeclaraColunaJsonbObrigatoria()
    {
        Propriedade.GetColumnType().Should().Be("jsonb");
        Propriedade.IsNullable.Should().BeFalse();
    }

    [Fact(DisplayName = "O valor gravado como texto bruto é lido de volta com o mesmo conteúdo")]
    public void Conversor_IdaEVolta_PreservaOConteudo()
    {
        using JsonDocument origem = JsonDocument.Parse("""{"limite":10,"faixas":["a","b"]}""");
        ValueConverter conversor = Propriedade.GetValueConverter()!;

        string gravado = (string)conversor.ConvertToProvider(origem.RootElement)!;
        JsonElement lido = (JsonElement)conversor.ConvertFromProvider(gravado)!;

        gravado.Should().Be("""{"limite":10,"faixas":["a","b"]}""");
        lido.GetRawText().Should().Be(gravado);
    }

    [Fact(DisplayName = "O valor lido continua válido depois que o documento de origem é descartado")]
    public void Desanexado_DocumentoDescartado_MantemOValor()
    {
        JsonElement lido = ColunaDeValorJson.Desanexado("""{"limite":10}""");

        lido.GetProperty("limite").GetInt32().Should().Be(10);
    }

    [Fact(DisplayName = "O documento malformado é recusado com mensagem fixa, sem o texto gravado")]
    public void Conversor_DocumentoMalformado_RecusaComMensagemFixa()
    {
        ValueConverter conversor = Propriedade.GetValueConverter()!;

        Action leitura = () => conversor.ConvertFromProvider("{\"" + Marcador + "\":");

        leitura.DeveRecusarComoDocumentoInvalido(NomeDoValor, Marcador);
    }

    [Fact(DisplayName = "O comparador distingue valores pelo texto e o instantâneo não depende da origem")]
    public void Comparador_ComparaPeloTextoEGeraInstantaneoIndependente()
    {
        JsonElement igual = ColunaDeValorJson.Desanexado("""{"limite":10}""");
        JsonElement outro = ColunaDeValorJson.Desanexado("""{"limite":11}""");
        ValueComparer comparador = Propriedade.GetValueComparer()!;

        comparador.Equals(igual, ColunaDeValorJson.Desanexado("""{"limite":10}""")).Should().BeTrue();
        comparador.Equals(igual, outro).Should().BeFalse();
        ((JsonElement)comparador.Snapshot(igual)!).GetRawText().Should().Be(igual.GetRawText());
    }

    private static IModel MontarModelo()
    {
        DbContextOptions<ContextoDeTeste> opcoes = new DbContextOptionsBuilder<ContextoDeTeste>()
            .UseNpgsql("Host=modelo-sem-banco;Database=modelo;Username=x;Password=x")
            .Options;
        using ContextoDeTeste contexto = new(opcoes);

        return contexto.GetService<IDesignTimeModel>().Model;
    }

    private sealed class EntidadeComValorJson
    {
        public Guid Id { get; set; }

        public JsonElement Valor { get; set; }
    }

    private sealed class ContextoDeTeste(DbContextOptions<ContextoDeTeste> opcoes) : DbContext(opcoes)
    {
        protected override void OnModelCreating(ModelBuilder modelBuilder) =>
            modelBuilder.Entity<EntidadeComValorJson>(entidade =>
            {
                entidade.HasKey(e => e.Id);
                entidade.MapearValorJson(e => e.Valor, NomeDoValor);
            });
    }
}
