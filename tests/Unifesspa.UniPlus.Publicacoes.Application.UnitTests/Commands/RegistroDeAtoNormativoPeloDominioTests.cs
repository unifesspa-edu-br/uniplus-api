namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using Microsoft.EntityFrameworkCore;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// A porta de escrita registra o ato no rastreador, sem confirmar, e recusa antes de qualquer gravação com o código que
/// nomeia a regra e o ato que a causa pela série, número e ano, nunca pelo identificador.
/// </summary>
public sealed class RegistroDeAtoNormativoPeloDominioTests
{
    private const string Hash = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Tipo = "EDITAL_ABERTURA";
    private const string Objeto = "PROCESSO_SELETIVO";
    private static readonly DateOnly DataDoAto = new(2026, 3, 10);
    private static readonly DateTimeOffset PublicadoEm = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly RegistroDeAtoNormativoPeloDominio _porta;

    public RegistroDeAtoNormativoPeloDominioTests() =>
        _porta = new(MontagemDeTeste.Criar(_tipos, _atos), _atos);

    [Fact(DisplayName = "Adiciona ao rastreador o ato com o identificador, a data e o instante recebidos")]
    public async Task Registrar_AtoValido_AdicionaSemRelerOCatalogo()
    {
        AtoNormativo? adicionado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => adicionado = a), Arg.Any<CancellationToken>());
        AtoNormativoPublicado ato = Ato();

        Result resultado = await _porta.RegistrarAsync(ato, CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        adicionado!.Id.Should().Be(ato.AtoId);
        adicionado.DataDoAto.Should().Be(DataDoAto);
        adicionado.PublicadoEm.Should().Be(PublicadoEm);
        adicionado.CongelaConfiguracao.Should().BeTrue();
        await _tipos.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
    }

    [Fact(DisplayName = "Aceita o ato que trata do processo seletivo, o registro que só o domínio faz")]
    public async Task Registrar_VinculoComProcessoSeletivo_Aceita()
    {
        Result resultado = await _porta.RegistrarAsync(
            Ato() with { Vinculos = [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Guid.CreateVersion7())] },
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
    }

    [Fact(DisplayName = "Guarda a versão invocada e o ato retificado quando o ato os declara")]
    public async Task Registrar_ComVersaoECorrecao_GuardaOsDois()
    {
        AtoNormativo retificado = AtoExistente(Tipo, congela: true);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        AtoNormativo? adicionado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => adicionado = a), Arg.Any<CancellationToken>());
        VersaoInvocadaDoAto versao = new(Guid.CreateVersion7(), Hash);

        Result resultado = await _porta.RegistrarAsync(
            Ato() with { VersaoInvocada = versao, Retificacao = new(retificado.Id, "corrige o anexo II") },
            CancellationToken.None);

        resultado.IsSuccess.Should().BeTrue();
        adicionado!.AtoRetificadoId.Should().Be(retificado.Id);
        adicionado.MotivoRetificacao.Should().Be("corrige o anexo II");
        adicionado.VersaoInvocada!.Id.Should().Be(versao.Id);
    }

    [Fact(DisplayName = "Recusa a vaga ocupada pelo ato do mesmo tipo e o nomeia pela numeração, sem gravar")]
    public async Task Registrar_VagaOcupada_RecusaCitandoNumeracaoSemGravar()
    {
        AtoNormativo ocupante = AtoExistente(Tipo, congela: true);
        _atos.ObterAtoConflitanteNoObjetoAsync(
                Objeto, Arg.Any<Guid>(), Tipo, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ocupante);

        Result resultado = await _porta.RegistrarAsync(Ato(unicoPorObjeto: true), CancellationToken.None);

        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        resultado.Error.Message.Should().Contain("EDITAL nº 13/2026").And.NotContain(ocupante.Id.ToString());
        await _atos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
        await _atos.DidNotReceiveWithAnyArgs().ReservarVagaAsync(default!, default);
    }

    [Fact(DisplayName = "Recusa a vaga ocupada por outra linhagem e nomeia o ato que a abriu pela numeração")]
    public async Task Registrar_VagaDeOutraLinhagem_RecusaCitandoARaiz()
    {
        AtoNormativo raiz = AtoExistente(Tipo, congela: true);
        _atos.ObterVagaDoObjetoAsync(Objeto, Arg.Any<Guid>(), Tipo, Arg.Any<CancellationToken>())
            .Returns(VagaDeLinhagem.Criar(raiz, raiz.Vinculos.First(), raiz.Id));
        _atos.ObterPorIdParaLeituraAsync(raiz.Id, Arg.Any<CancellationToken>()).Returns(raiz);

        Result resultado = await _porta.RegistrarAsync(Ato(unicoPorObjeto: true), CancellationToken.None);

        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        resultado.Error.Message.Should().Contain("EDITAL nº 13/2026").And.NotContain(raiz.Id.ToString());
    }

    [Fact(DisplayName = "Recusa a retificação do ato já retificado e nomeia o retificador pela numeração, sem GUID")]
    public async Task Registrar_AtoJaRetificado_RecusaCitandoORetificador()
    {
        AtoNormativo retificado = AtoExistente(Tipo, congela: true);
        AtoNormativo retificador = AtoExistente(Tipo, congela: true, numero: "14");
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificador);

        Result resultado = await _porta.RegistrarAsync(
            Ato() with { Retificacao = new(retificado.Id, "corrige o anexo II") }, CancellationToken.None);

        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.AtoJaRetificado);
        resultado.Error.Message.Should().Contain("EDITAL nº 14/2026")
            .And.NotContain(retificado.Id.ToString()).And.NotContain(retificador.Id.ToString());
        await _atos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Fact(DisplayName = "Recusa o anexo com hash inválido antes de qualquer leitura")]
    public async Task Registrar_AnexoComHashInvalido_Recusa()
    {
        Result resultado = await _porta.RegistrarAsync(
            Ato() with { Anexos = [new("anexo.pdf", "application/pdf", "não-é-hash")] }, CancellationToken.None);

        resultado.IsFailure.Should().BeTrue();
        await _atos.DidNotReceiveWithAnyArgs().AdicionarAsync(default!, default);
    }

    [Theory(DisplayName = "Traduz a violação de unicidade do registro de atos na recusa que a nomeia")]
    [InlineData("ux_vagas_de_linhagem_objeto_tipo", AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo)]
    [InlineData("ux_ato_normativo_retificado", AtoNormativoErrorCodes.AtoJaRetificado)]
    public void TraduzirConflitoDaGravacao_ConstraintDoRegistro_NomeiaARecusa(string constraint, string codigo)
    {
        DbUpdateException violacao = new("falha", PostgresExceptionFactory.Create("23505", constraint));

        DomainError? recusa = _porta.TraduzirConflitoDaGravacao(violacao);

        recusa!.Code.Should().Be(codigo);
    }

    [Fact(DisplayName = "Não reconhece a unicidade de outra tabela nem a exceção que não é de gravação")]
    public void TraduzirConflitoDaGravacao_ForaDoRegistro_NaoReconhece()
    {
        DbUpdateException deOutraTabela = new("falha", PostgresExceptionFactory.Create("23505", "ux_atos_do_processo_ato_corrigido"));

        _porta.TraduzirConflitoDaGravacao(deOutraTabela).Should().BeNull();
        _porta.TraduzirConflitoDaGravacao(new InvalidOperationException("outra")).Should().BeNull();
    }

    private static AtoNormativoPublicado Ato(bool unicoPorObjeto = false) =>
        new()
        {
            AtoId = Guid.CreateVersion7(),
            Orgao = "CEPS",
            Serie = "EDITAL",
            Ano = 2026,
            Numero = "13",
            TipoCodigo = Tipo,
            AtributosDoTipo = new(CongelaConfiguracao: true, unicoPorObjeto, EfeitoIrreversivel: false, "Edital de abertura"),
            DataDoAto = DataDoAto,
            PublicadoEm = PublicadoEm,
            DocumentoHash = Hash,
            Assinante = "Jairo Belchior",
            Vinculos = [new(Objeto, ProcessoDoTeste)],
        };

    private static readonly Guid ProcessoDoTeste = Guid.CreateVersion7();

    private static AtoNormativo AtoExistente(string tipo, bool congela, string numero = "13") =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(), "CEPS", "EDITAL", 2026, numero, tipo,
            congelaConfiguracao: congela, efeitoIrreversivel: false, unicoPorObjeto: true,
            DataDoAto, Hash, "Jairo Belchior", PublicadoEm, versaoInvocada: null, vinculos: [(Objeto, ProcessoDoTeste)]);
}
