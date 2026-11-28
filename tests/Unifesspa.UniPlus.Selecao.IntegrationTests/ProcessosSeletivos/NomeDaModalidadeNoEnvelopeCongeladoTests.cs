namespace Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos;

using System.Text;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Application.Abstractions;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

using Xunit;

using static Unifesspa.UniPlus.Selecao.IntegrationTests.ProcessosSeletivos.ExemploDeProcessoSeletivoVagas;

/// <summary>
/// O nome da modalidade faz parte do conteúdo congelado: o mesmo processo, canonicalizado duas vezes e diferindo só
/// no nome de uma modalidade, não pode ter os mesmos bytes nem o mesmo resumo SHA-256.
/// </summary>
public sealed class NomeDaModalidadeNoEnvelopeCongeladoTests
{
    private const string NomeDaAmpla = "Concorrencia ampla do teste";
    private const string NomeDaAmplaRenomeada = "Concorrencia geral do teste";

    [Fact(DisplayName = "Dois envelopes do mesmo processo, só com nome de modalidade diferente, têm bytes e resumo SHA-256 diferentes")]
    public void NomeDeModalidadeDiferente_MudaBytesEResumoDoEnvelope()
    {
        SnapshotCanonico original = CanonicalizarComANomeDaAmpla(NomeDaAmpla);
        SnapshotCanonico renomeado = CanonicalizarComANomeDaAmpla(NomeDaAmplaRenomeada);

        renomeado.Bytes.Should().NotEqual(original.Bytes);
        HashCanonicalComputer.ComputeSha256Hex(renomeado.Bytes).Should().NotBe(HashCanonicalComputer.ComputeSha256Hex(original.Bytes));
        Encoding.UTF8.GetString(original.Bytes).Should().Contain(NomeDaAmpla).And.NotContain(NomeDaAmplaRenomeada);
        Encoding.UTF8.GetString(renomeado.Bytes).Should().Contain(NomeDaAmplaRenomeada).And.NotContain(NomeDaAmpla);
    }

    private static SnapshotCanonico CanonicalizarComANomeDaAmpla(string nome)
    {
        ProcessoSeletivo processo = ExemploDeProcessoSeletivoProcessoRico.ProcessoRico();
        processo.DefinirDistribuicaoVagas(
            [DistribuicaoLei12711(nomeDaModalidadeAmpla: nome), DistribuicaoLei12711(ExemploDeProcessoSeletivo.OfertaDireito, ("HUMANISTICA_I", "Humanística I"))],
            FatosDeModalidadeDeTeste.DoCatalogo).IsSuccess.Should().BeTrue();

        return ExemploDeProcessoSeletivo.Canonicalizador.Canonicalizar(ExemploDeProcessoSeletivoEntrada.Entrada(processo));
    }
}
