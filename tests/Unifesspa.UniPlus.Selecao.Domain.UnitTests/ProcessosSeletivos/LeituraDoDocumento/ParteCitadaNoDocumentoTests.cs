namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;

using Xunit;

using static Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.LeituraDoDocumento.DocumentoLidoDeTeste;

/// <summary>A chave citada por outra parte é a de uma parte adotada, a de uma recusada ou a que o documento não tem, tanto para a fase quanto para a etapa.</summary>
public sealed class ParteCitadaNoDocumentoTests
{
    private static readonly Guid IdDaParte = Guid.CreateVersion7();

    private static readonly Dictionary<string, Guid> Adotadas = new(StringComparer.Ordinal) { ["parte_a"] = IdDaParte };
    private static readonly HashSet<string> Recusadas = new(StringComparer.Ordinal) { "parte_recusada" };

    [Fact(DisplayName = "A chave da parte adotada traz o id dela")]
    public void ChaveAdotada_TrazOId()
    {
        ParteCitadaNoDocumento parte = ParteCitadaNoDocumento.De("parte_a", Adotadas, Recusadas);

        parte.Id.Should().Be(IdDaParte);
        parte.RecusadaNaLeitura.Should().BeFalse();
        parte.Inexistente.Should().BeFalse();
    }

    [Fact(DisplayName = "A chave da parte recusada não traz id e não é inexistente")]
    public void ChaveRecusada_NaoTrazIdENaoEInexistente()
    {
        ParteCitadaNoDocumento parte = ParteCitadaNoDocumento.De("parte_recusada", Adotadas, Recusadas);

        parte.Id.Should().BeNull();
        parte.RecusadaNaLeitura.Should().BeTrue();
        parte.Inexistente.Should().BeFalse();
    }

    [Fact(DisplayName = "A chave que o documento não tem é inexistente")]
    public void ChaveDesconhecida_EInexistente()
    {
        ParteCitadaNoDocumento.De("outra", Adotadas, Recusadas).Inexistente.Should().BeTrue();
    }

    [Fact(DisplayName = "A fase adotada e a fase recusada pela leitura são distinguidas da fase que o documento não tem")]
    public void Fases_SaoDistinguidas()
    {
        FaseNoDocumento[] fases = [Fase(0, "inscricao"), Fase(1, "digitada", "FASE-DIGITADA")];
        FasesLidas lidas = LeituraDasFases.Ler(fases, Cadastros());
        FaseCronograma adotada = lidas.Fases.Single();

        ParteCitadaNoDocumento.De("inscricao", lidas).Id.Should().Be(adotada.Id);
        ParteCitadaNoDocumento.De("digitada", lidas).RecusadaNaLeitura.Should().BeTrue();
        ParteCitadaNoDocumento.De("ausente", lidas).Inexistente.Should().BeTrue();
    }

    [Fact(DisplayName = "A etapa adotada e a etapa recusada pela leitura são distinguidas da etapa que o documento não tem")]
    public void Etapas_SaoDistinguidas()
    {
        EtapasLidas lidas = EtapasLidasDeTeste.Ler(EtapasLidasDeTeste.Etapa(0, "etapa_a"), EtapasLidasDeTeste.Etapa(1, "etapa_b", tipo: "TIPO-DIGITADO"));

        ParteCitadaNoDocumento.De("etapa_a", lidas).Id.Should().Be(lidas.Etapas.Single().Id);
        ParteCitadaNoDocumento.De("etapa_b", lidas).RecusadaNaLeitura.Should().BeTrue();
        ParteCitadaNoDocumento.De("ausente", lidas).Inexistente.Should().BeTrue();
    }

    [Fact(DisplayName = "A chave repetida entre as fases adotadas fica com a primeira")]
    public void ChaveRepetida_FicaComAPrimeira()
    {
        Dictionary<string, Guid> ids = ChavesDasPartesLidas.IdPelaChave([("chave", IdDaParte), ("chave", Guid.CreateVersion7())]);

        ids.Should().ContainSingle().Which.Value.Should().Be(IdDaParte);
    }
}
