namespace Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Configuracao.Domain.Entities;
using Unifesspa.UniPlus.Configuracao.Domain.Enums;
using Unifesspa.UniPlus.Configuracao.Domain.Errors;
using Unifesspa.UniPlus.Kernel.Results;

using static Unifesspa.UniPlus.Configuracao.Domain.UnitTests.Entities.ApoioDaModalidade;

/// <summary>Atualização de <see cref="Modalidade"/> institucional: código e identidade imutáveis, revalidação da coerência e liberdade de alterar a estrutura.</summary>
public sealed class ModalidadeAtualizacaoTests
{
    [Fact(DisplayName = "Atualizar para referenciar a si mesma é rejeitado")]
    public void Atualizar_ReferenciandoSiMesma_Falha()
    {
        // Código fora do catálogo legal fixo: as modalidades protegidas recusam qualquer
        // alteração estrutural antes de chegar às invariantes de coerência.
        Modalidade m = Criar("PSIQ_REGIONAL", d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoPar = "AC_Q" }).Value!;

        Result resultado = m.Atualizar(Declarar(d => d.ComoAcaoAfirmativa("CRUZADO") with { RemanejamentoPar = "PSIQ_REGIONAL" }));

        resultado.IsFailure.Should().BeTrue("a recusa vale nos dois caminhos de escrita");
        resultado.Errors.Should().Contain(e => e.Error.Code == "Modalidade.RemanejamentoParCircular");
    }

    // ── Imutabilidade do código na atualização ─────────────────────────────────

    [Fact(DisplayName = "Atualizar troca atributos editáveis mantendo Codigo e Id imutáveis")]
    public void Atualizar_MantemCodigoEId()
    {
        Modalidade m = Criar("PSIQ_INDIGENA", d => d.ComoCotaReservada()).Value!;
        Guid idOriginal = m.Id;

        Result r = m.Atualizar(Declarar(d => d with { Descricao = "Nova descrição" }));

        r.IsSuccess.Should().BeTrue();
        m.Codigo.Valor.Should().Be("PSIQ_INDIGENA", "o código é imutável");
        m.Id.Should().Be(idOriginal, "o Id é imutável");
        m.NaturezaLegal.Should().Be(NaturezaLegal.Ampla);
        m.Descricao.Should().Be("Nova descrição");
    }

    [Fact(DisplayName = "Atualizar revalida coerência (cota sem cascata falha)")]
    public void Atualizar_Incoerente_Falha()
    {
        Modalidade m = Criar("PSVR_AMPLA").Value!;

        Result r = m.Atualizar(Declarar(d => d with { NaturezaLegalToken = "COTA_RESERVADA", ComposicaoVagasToken = "DENTRO_DA_RESERVA_LEGAL" }));

        r.IsFailure.Should().BeTrue();
        r.Error!.Code.Should().Be(ModalidadeErrorCodes.NaturezaRemanejamentoIncoerente);
    }

    [Fact(DisplayName = "Modalidade institucional segue alterando a estrutura livremente")]
    public void Atualizar_Institucional_AlteraEstrutura()
    {
        Modalidade m = Criar("PSIQ_QUILOMBOLA", d => d.ComoCotaReservada()).Value!;

        Result r = m.Atualizar(Declarar(d => d.ComoAcaoAfirmativa("DESTINO_UNICO") with { RemanejamentoDestino = "AC" }));

        r.IsSuccess.Should().BeTrue();
        m.NaturezaLegal.Should().Be(NaturezaLegal.AcaoAfirmativa);
    }
}
