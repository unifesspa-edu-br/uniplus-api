namespace Unifesspa.UniPlus.Publicacoes.Domain.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;

/// <summary>Os vínculos do ato conferidos por posição: nulo, forma, identificador e repetição.</summary>
public sealed class VinculosDoAtoTests
{
    private static readonly Guid ProcessoId = Guid.CreateVersion7();
    private static readonly EntidadeVinculada Processo = new("PROCESSO", ProcessoId);
    private static readonly EntidadeVinculada Chamada = new("CHAMADA", Guid.CreateVersion7());

    private static IEnumerable<FieldError> Recusas(params EntidadeVinculada?[] vinculos) =>
        AtoNormativo.ConferirVinculos(vinculos).SelectMany(parte => parte.Recusas);

    [Fact(DisplayName = "Sem vínculos não há recusa")]
    public void ConferirVinculos_Vazio_SemRecusa()
    {
        AtoNormativo.ConferirVinculos([]).Should().BeEmpty();
    }

    [Fact(DisplayName = "Vínculos diferentes são aceitos, uma parte por posição")]
    public void ConferirVinculos_Diferentes_Aceitos()
    {
        IReadOnlyList<ParteConferida<EntidadeVinculada>> partes = AtoNormativo.ConferirVinculos([Processo, Chamada]);

        partes.Select(parte => parte.Valor).Should().Equal(Processo, Chamada);
    }

    [Fact(DisplayName = "O mesmo identificador sob rótulos diferentes não é repetição")]
    public void ConferirVinculos_MesmoIdRotulosDiferentes_NaoRepete()
    {
        Recusas(Processo, new EntidadeVinculada("CHAMADA", ProcessoId)).Should().BeEmpty();
    }

    [Fact(DisplayName = "A segunda ocorrência da mesma entidade é recusada pela posição, sem escrever o tipo nem o identificador")]
    public void ConferirVinculos_Repetido_RecusaASegundaOcorrencia()
    {
        FieldError[] recusas = [.. Recusas(Processo, Chamada, Processo)];

        ResumoDasRecusas.De(recusas).Should().Equal(("vinculos[2]", AtoNormativoErrorCodes.VinculoRepetido));
        recusas[0].Error.Message.Should().Be("A mesma entidade foi vinculada mais de uma vez a este ato. Retire a repetição.")
            .And.NotContain("PROCESSO").And.NotContain(ProcessoId.ToString());
    }

    [Fact(DisplayName = "Cada repetição seguinte é recusada")]
    public void ConferirVinculos_RepetidoTresVezes_RecusaAsDuasUltimas()
    {
        ResumoDasRecusas.De(Recusas(Processo, Processo, Processo)).Should().Equal(
            ("vinculos[1]", AtoNormativoErrorCodes.VinculoRepetido),
            ("vinculos[2]", AtoNormativoErrorCodes.VinculoRepetido));
    }

    [Fact(DisplayName = "Elemento nulo é recusado na posição, sem recusa de tipo nem de identificador")]
    public void ConferirVinculos_Nulo_RecusaNaPosicao()
    {
        FieldError[] recusas = [.. Recusas(Processo, null, Chamada)];

        ResumoDasRecusas.De(recusas).Should().Equal(("vinculos[1]", AtoNormativoErrorCodes.VinculoNulo));
        recusas[0].Error.Message.Should().Be("Vínculo nulo na lista de vínculos; retire o elemento ou preencha.");
    }

    [Fact(DisplayName = "Vínculos nulos, malformados e repetidos são recusados juntos, por posição")]
    public void ConferirVinculos_VariasRecusas_AcumulaPorPosicao()
    {
        ResumoDasRecusas.De(Recusas(new EntidadeVinculada("x", Guid.Empty), null, Chamada, Chamada)).Should().Equal(
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido),
            ("vinculos[0].entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade),
            ("vinculos[1]", AtoNormativoErrorCodes.VinculoNulo),
            ("vinculos[3]", AtoNormativoErrorCodes.VinculoRepetido));
    }

    [Fact(DisplayName = "Dois pares iguais com tipo malformado recusam a forma de cada um e a repetição do segundo, depois da forma dele")]
    public void ConferirVinculos_MalformadoRepetido_RecusaFormaERepeticao()
    {
        EntidadeVinculada malformado = new("x", ProcessoId);

        ResumoDasRecusas.De(Recusas(malformado, malformado)).Should().Equal(
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido),
            ("vinculos[1].entidadeTipo", AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido),
            ("vinculos[1]", AtoNormativoErrorCodes.VinculoRepetido));
    }

    [Fact(DisplayName = "Repetição com identificador vazio recusa o identificador de cada um e a repetição do segundo")]
    public void ConferirVinculos_IdVazioRepetido_RecusaIdERepeticao()
    {
        EntidadeVinculada semId = new("PROCESSO", Guid.Empty);

        ResumoDasRecusas.De(Recusas(semId, semId)).Should().Equal(
            ("vinculos[0].entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade),
            ("vinculos[1].entidadeId", AtoNormativoErrorCodes.VinculoSemIdentificadorDaEntidade),
            ("vinculos[1]", AtoNormativoErrorCodes.VinculoRepetido));
    }

    [Fact(DisplayName = "Tipo nulo repetido é recusado sem lançar")]
    public void ConferirVinculos_TipoNuloRepetido_RecusaSemLancar()
    {
        EntidadeVinculada semTipo = new(null!, ProcessoId);

        ResumoDasRecusas.De(Recusas(semTipo, semTipo)).Select(r => r.Codigo).Should().Equal(
            AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido,
            AtoNormativoErrorCodes.VinculoTipoDeEntidadeInvalido,
            AtoNormativoErrorCodes.VinculoRepetido);
    }
}
