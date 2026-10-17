namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

public sealed class AtoDoProcessoTests
{
    private static readonly DateTimeOffset Agora = new(2026, 12, 10, 12, 0, 0, TimeSpan.Zero);

    private static NovoAtoDoProcesso Novo(
        Guid? atoCorrigidoId = null,
        string? motivo = null,
        Guid? principal = null,
        IReadOnlyList<AnexoDeclarado>? anexos = null,
        bool politicaConfirmada = false) =>
        new(
            AtoId: Guid.CreateVersion7(),
            ProcessoSeletivoId: Guid.CreateVersion7(),
            TipoCodigo: "AVISO",
            TipoNome: "Aviso",
            Numero: "3/2026",
            DataDoAto: new DateOnly(2026, 12, 10),
            Assinante: "Diretor do CEPS",
            UnidadeAdministradora: "ceps",
            IdentificadorLegivelDoProcesso: "psm-medicina-2027",
            DocumentoPrincipalId: principal ?? Guid.CreateVersion7(),
            Anexos: anexos ?? [],
            AtoCorrigidoId: atoCorrigidoId,
            MotivoDaCorrecao: motivo,
            PoliticaDeDivulgacaoConfirmada: politicaConfirmada);

    [Fact(DisplayName = "O registro nasce pendente, com os anexos na ordem declarada e a confirmação da política datada")]
    public void Registrar_NascePendenteComAnexosNaOrdemDeclarada()
    {
        Guid segundo = Guid.CreateVersion7();
        Guid primeiro = Guid.CreateVersion7();
        NovoAtoDoProcesso novo = Novo(
            anexos: [new AnexoDeclarado(segundo, "Anexo B"), new AnexoDeclarado(primeiro, "Anexo A")],
            politicaConfirmada: true);

        AtoDoProcesso ato = AtoDoProcesso.Registrar(novo, new RelogioFixo(Agora));

        ato.Id.Should().Be(novo.AtoId, "o registro é identificado pelo ato que a Seleção decidiu");
        ato.Situacao.Should().Be(SituacaoDoRegistroDoAto.Pendente);
        ato.RegistradoEm.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaEm.Should().Be(Agora);
        ato.Anexos.Select(a => (a.Ordem, a.DocumentoAPublicarId, a.NomeDeExibicao))
            .Should().Equal((0, segundo, "Anexo B"), (1, primeiro, "Anexo A"));
    }

    [Fact(DisplayName = "Confirmar o registro marca o instante uma vez só: a repetição não muda nada")]
    public void ConfirmarRegistro_Repetido_NaoMudaNada()
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(Novo(), new RelogioFixo(Agora));

        ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(1))).Should().BeTrue();
        ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(9))).Should().BeFalse();

        ato.Situacao.Should().Be(SituacaoDoRegistroDoAto.Registrado);
        ato.RegistradoEm.Should().Be(Agora.AddMinutes(1));
    }

    [Fact(DisplayName = "O ato corrigido e o motivo vêm juntos")]
    public void Registrar_AtoCorrigidoSemMotivo_Lanca()
    {
        Func<AtoDoProcesso> semMotivo = () => AtoDoProcesso.Registrar(Novo(atoCorrigidoId: Guid.CreateVersion7()), TimeProvider.System);
        Func<AtoDoProcesso> semAto = () => AtoDoProcesso.Registrar(Novo(motivo: "Erro de grafia"), TimeProvider.System);

        semMotivo.Should().Throw<ArgumentException>();
        semAto.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Um documento não é o principal e anexo, nem anexo duas vezes")]
    public void Registrar_DocumentoRepetido_Lanca()
    {
        Guid principal = Guid.CreateVersion7();
        Guid anexo = Guid.CreateVersion7();

        Func<AtoDoProcesso> principalComoAnexo = () => AtoDoProcesso.Registrar(
            Novo(principal: principal, anexos: [new AnexoDeclarado(principal, "Anexo")]), TimeProvider.System);
        Func<AtoDoProcesso> anexoDuasVezes = () => AtoDoProcesso.Registrar(
            Novo(anexos: [new AnexoDeclarado(anexo, "Anexo"), new AnexoDeclarado(anexo, "Outro")]), TimeProvider.System);

        principalComoAnexo.Should().Throw<ArgumentException>();
        anexoDuasVezes.Should().Throw<ArgumentException>();
    }
}
