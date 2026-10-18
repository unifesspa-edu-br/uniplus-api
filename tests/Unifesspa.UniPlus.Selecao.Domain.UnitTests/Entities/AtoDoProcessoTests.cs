namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.Entities;

using AwesomeAssertions;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
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
        string? politicaConfirmadaPor = null) =>
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
            PoliticaDeDivulgacaoConfirmadaPorSub: politicaConfirmadaPor);

    [Fact(DisplayName = "O registro nasce pendente, com os anexos na ordem declarada e a confirmação da política datada e atribuída")]
    public void Registrar_NascePendenteComAnexosNaOrdemDeclarada()
    {
        Guid segundo = Guid.CreateVersion7();
        Guid primeiro = Guid.CreateVersion7();
        NovoAtoDoProcesso novo = Novo(
            anexos: [new AnexoDeclarado(segundo, "Anexo B"), new AnexoDeclarado(primeiro, "Anexo A")],
            politicaConfirmadaPor: "sub-do-usuario");

        AtoDoProcesso ato = AtoDoProcesso.Registrar(novo, new RelogioFixo(Agora)).Value!;

        ato.Id.Should().Be(novo.AtoId, "o registro é identificado pelo ato que a Seleção decidiu");
        ato.Situacao.Should().Be(SituacaoDoRegistroDoAto.Pendente);
        ato.RegistradoEm.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaEm.Should().Be(Agora);
        ato.PoliticaDeDivulgacaoConfirmadaPorSub.Should().Be("sub-do-usuario");
        ato.Anexos.Select(a => (a.Ordem, a.DocumentoAPublicarId, a.NomeDeExibicao))
            .Should().Equal((0, segundo, "Anexo B"), (1, primeiro, "Anexo A"));
    }

    [Fact(DisplayName = "Confirmar o registro marca o instante uma vez só: a repetição não muda nada")]
    public void ConfirmarRegistro_Repetido_NaoMudaNada()
    {
        AtoDoProcesso ato = AtoDoProcesso.Registrar(Novo(), new RelogioFixo(Agora)).Value!;

        ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(1))).Should().BeTrue();
        ato.ConfirmarRegistro(new RelogioFixo(Agora.AddMinutes(9))).Should().BeFalse();

        ato.Situacao.Should().Be(SituacaoDoRegistroDoAto.Registrado);
        ato.RegistradoEm.Should().Be(Agora.AddMinutes(1));
    }

    [Fact(DisplayName = "Ato corrigido sem motivo é recusado com falha nomeada no campo do motivo")]
    public void Registrar_AtoCorrigidoSemMotivo_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(atoCorrigidoId: Guid.CreateVersion7()), TimeProvider.System);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "motivoDaCorrecao"
                && e.Error.Code == AtoDoProcessoErrorCodes.MotivoDaCorrecaoObrigatorio);
    }

    [Fact(DisplayName = "Motivo sem ato corrigido é recusado com falha nomeada no campo do ato")]
    public void Registrar_MotivoSemAtoCorrigido_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(motivo: "Erro de grafia"), TimeProvider.System);

        resultado.IsFailure.Should().BeTrue();
        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "atoCorrigidoId"
                && e.Error.Code == AtoDoProcessoErrorCodes.AtoCorrigidoObrigatorio);
    }

    [Fact(DisplayName = "Ato corrigido com identificador vazio é recusado com falha nomeada")]
    public void Registrar_AtoCorrigidoVazio_DevolveFalhaNomeada()
    {
        Result<AtoDoProcesso> resultado = AtoDoProcesso.Registrar(Novo(atoCorrigidoId: Guid.Empty, motivo: "Erro de grafia"), TimeProvider.System);

        resultado.Errors.Should().ContainSingle()
            .Which.Should().Match<FieldError>(e => e.Field == "atoCorrigidoId"
                && e.Error.Code == AtoDoProcessoErrorCodes.AtoCorrigidoInvalido);
    }

    [Fact(DisplayName = "Um ato não corrige a si mesmo: o identificador é do servidor, então a violação é de programação")]
    public void Registrar_AtoQueCorrigeASiMesmo_Lanca()
    {
        NovoAtoDoProcesso novo = Novo(motivo: "Erro de grafia");
        Func<Result<AtoDoProcesso>> registrar = () => AtoDoProcesso.Registrar(novo with { AtoCorrigidoId = novo.AtoId }, TimeProvider.System);

        registrar.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Um documento não é o principal e anexo, nem anexo duas vezes")]
    public void Registrar_DocumentoRepetido_Lanca()
    {
        Guid principal = Guid.CreateVersion7();
        Guid anexo = Guid.CreateVersion7();

        Func<Result<AtoDoProcesso>> principalComoAnexo = () => AtoDoProcesso.Registrar(
            Novo(principal: principal, anexos: [new AnexoDeclarado(principal, "Anexo")]), TimeProvider.System);
        Func<Result<AtoDoProcesso>> anexoDuasVezes = () => AtoDoProcesso.Registrar(
            Novo(anexos: [new AnexoDeclarado(anexo, "Anexo"), new AnexoDeclarado(anexo, "Outro")]), TimeProvider.System);

        principalComoAnexo.Should().Throw<ArgumentException>();
        anexoDuasVezes.Should().Throw<ArgumentException>();
    }

    [Fact(DisplayName = "Texto opcional em branco é ausência: vira nulo, sem exceção")]
    public void Registrar_TextoOpcionalEmBranco_ViraNulo()
    {
        NovoAtoDoProcesso novo = Novo(politicaConfirmadaPor: "  ") with { Numero = "   " };

        AtoDoProcesso ato = AtoDoProcesso.Registrar(novo, new RelogioFixo(Agora)).Value!;

        ato.Numero.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaPorSub.Should().BeNull();
        ato.PoliticaDeDivulgacaoConfirmadaEm.Should().BeNull();
        ato.MotivoDaCorrecao.Should().BeNull();
    }
}
