namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Application.UnitTests.TestSupport;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;
using Unifesspa.UniPlus.Publicacoes.Domain.ValueObjects;
using Unifesspa.UniPlus.Testes.Compartilhado;

/// <summary>
/// O vínculo genérico no registro do ato (ADR-0105) e a vaga que a linhagem reserva
/// (ADR-0107), do ponto de vista do handler: a herança de vínculos pela retificação, a
/// reserva da vaga, e a recusa quando o objeto já é tratado por outra linhagem.
/// </summary>
public sealed class RegistrarAtoNormativoVinculosTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Tipo = "EDITAL_ABERTURA";
    private const string Objeto = "OBJETO_A";
    private static readonly DateOnly DataDoAto = new(2026, 3, 10);
    private static readonly DateOnly DiaDaPublicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid Processo = Guid.CreateVersion7();
    private static readonly AtoNormativoDeTeste AtoDoObjeto = new()
    {
        TipoCodigo = Tipo,
        CongelaConfiguracao = true,
        UnicoPorObjeto = true,
        DataDoAto = DataDoAto,
        Vinculos = [new(Objeto, Processo)],
    };

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(Agora);
    private AtoNormativo? _registrado;
    private VagaDeLinhagem? _vagaReservada;

    public RegistrarAtoNormativoVinculosTests()
    {
        _atos.When(a => a.AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>()))
            .Do(chamada => _registrado = chamada.Arg<AtoNormativo>());
        _atos.When(a => a.ReservarVagaAsync(Arg.Any<VagaDeLinhagem>(), Arg.Any<CancellationToken>()))
            .Do(chamada => _vagaReservada = chamada.Arg<VagaDeLinhagem>());
    }

    [Fact(DisplayName = "Registra o ato com os vínculos declarados")]
    public async Task Handle_ComVinculos_Registra()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando(vinculos: [new(Objeto, Processo)]));

        resultado.IsSuccess.Should().BeTrue();
        _registrado!.Vinculos.Should().ContainSingle()
            .Which.EntidadeId.Should().Be(Processo);
    }

    [Fact(DisplayName = "Tipo não único por objeto não reserva vaga alguma")]
    public async Task Handle_TipoNaoUnico_NaoReservaVaga()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        await Executar(Comando(vinculos: [new(Objeto, Processo)]));

        await _atos.DidNotReceive().ReservarVagaAsync(
            Arg.Any<VagaDeLinhagem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo único por objeto reserva a vaga em nome da própria raiz")]
    public async Task Handle_TipoUnico_ReservaVaga()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();
        VagaLivre();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: [new(Objeto, Processo)]));

        resultado.IsSuccess.Should().BeTrue();
        _vagaReservada.Should().NotBeNull();
        _vagaReservada!.RaizId.Should().Be(_registrado!.Id, "o ato não emenda ninguém: a linhagem nasce nele");
        _vagaReservada.EntidadeId.Should().Be(Processo);
        _vagaReservada.TipoCodigo.Should().Be(Tipo);
    }

    [Fact(DisplayName = "Tipo único por objeto sem vínculo não reserva vaga — sem objeto, não há vaga")]
    public async Task Handle_TipoUnicoSemVinculo_NaoReservaVaga()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando(vinculos: null));

        resultado.IsSuccess.Should().BeTrue();
        await _atos.DidNotReceive().ReservarVagaAsync(
            Arg.Any<VagaDeLinhagem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa com 409 quando outra linhagem já trata o objeto com um ato do mesmo tipo")]
    public async Task Handle_ObjetoTratadoPorOutraLinhagem_Falha()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        VagaLivre();

        AtoNormativo deOutraLinhagem = AtoDeOutraLinhagem();
        ConflitoNoObjetoCom(deOutraLinhagem);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando(vinculos: [new(Objeto, Processo)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        resultado.Error.Message.Should().Contain("EDITAL nº 99/2026").And.NotContain(deOutraLinhagem.Id.ToString());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <remarks>
    /// A retificação não declara vínculo nenhum: sem herança, sumiria da consulta do certame, e a
    /// consulta exibiria a versão superada escondendo a que a emenda.
    /// </remarks>
    [Fact(DisplayName = "A retificação herda os vínculos do ato que emenda")]
    public async Task Handle_Retificacao_HerdaVinculos()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        AtoNormativo retificado = RetificadoComVinculo();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(ComandoRetificando(retificado));

        resultado.IsSuccess.Should().BeTrue();
        _registrado!.Vinculos.Should().ContainSingle()
            .Which.EntidadeId.Should().Be(Processo);
    }

    [Fact(DisplayName = "A retificação reserva a vaga em nome da raiz da sua linhagem, não de si mesma")]
    public async Task Handle_RetificacaoDeTipoUnico_ReservaVagaEmNomeDaRaiz()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();
        VagaLivre();

        AtoNormativo retificado = RetificadoComVinculo();
        Guid raiz = Guid.CreateVersion7();
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([raiz, retificado.Id]);
        _atos.ObterRaizDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(raiz);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(ComandoRetificando(retificado));

        resultado.IsSuccess.Should().BeTrue();
        _vagaReservada!.RaizId.Should().Be(raiz, "a vaga é da linhagem, e a linhagem é identificada pela sua raiz");
    }

    [Fact(DisplayName = "A vaga já ocupada pela própria linhagem não é reservada de novo")]
    public async Task Handle_VagaDaPropriaLinhagem_NaoReservaDeNovo()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();

        AtoNormativo retificado = RetificadoQueERaiz();
        AtoNormativo raizDaVaga = AtoRetificado();
        _atos.ObterVagaDoObjetoAsync(new EntidadeVinculada(Objeto, Processo), Tipo, Arg.Any<CancellationToken>())
            .Returns(VagaDeLinhagem.Criar(raizDaVaga, raizDaVaga.Vinculos.First(), retificado.Id));

        Result<RegistrarAtoNormativoResult> resultado = await Executar(ComandoRetificando(retificado));

        resultado.IsSuccess.Should().BeTrue();
        await _atos.DidNotReceive().ReservarVagaAsync(
            Arg.Any<VagaDeLinhagem>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação pelo endpoint de um ato de processo seletivo é recusada, mesmo sem declarar o vínculo")]
    public async Task Handle_RetificacaoHerdaProcessoSeletivoPeloEndpoint_Falha()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        AtoNormativo retificado = RetificadoComVinculo();
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>())
            .Returns([new EntidadeVinculada(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)]);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(ComandoRetificando(retificado));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("atoRetificadoId", AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio));
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "O vínculo proibido declarado é recusado junto com a recusa do ato retificado")]
    public async Task Handle_VinculoProibidoDeclarado_RecusaJuntoComAsDemais()
    {
        Vigente(unico: false);
        Guid inexistente = Guid.CreateVersion7();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(
                vinculos: [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)],
                atoRetificadoId: inexistente,
                motivo: "corrige o anexo"));

        resultado.Errors.Select(recusa => (recusa.Field, recusa.Error.Code)).Should().Equal(
            ("atoRetificadoId", AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado),
            ("vinculos[0].entidadeTipo", AtoNormativoErrorCodes.VinculoExigeRegistroPeloDominio));
        await _atos.DidNotReceiveWithAnyArgs().ListarVinculosDoAtoAsync(default, default);
    }

    [Fact(DisplayName = "O registro por mensagem do domínio continua aceitando o vínculo com processo seletivo")]
    public async Task Handle_RegistroPeloDominio_AceitaProcessoSeletivo()
    {
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: [new(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, Processo)]) with
            {
                AtributosDoTipo = new AtributosDoTipoAto(true, false, false, "Edital de abertura"),
                ChegouPeloDominioDono = true,
            });

        resultado.IsSuccess.Should().BeTrue();
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, MontagemDeTeste.Criar(_tipos, _atos), _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void Vigente(bool unico)
    {
        TipoAtoPublicado tipo = new TipoAtoPublicadoDeTeste
        {
            Codigo = Tipo,
            CongelaConfiguracao = true,
            UnicoPorObjeto = unico,
        }.Criar().Value!;
        _tipos.ObterVigenteAsync(Tipo, DiaDaPublicacao, Arg.Any<CancellationToken>()).Returns(tipo);
    }

    private AtoNormativo RetificadoComVinculo()
    {
        AtoNormativo retificado = AtoRetificado();
        _atos.ComoRetificavel(retificado);
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([retificado.Id]);
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([new EntidadeVinculada(Objeto, Processo)]);
        return retificado;
    }

    private AtoNormativo RetificadoQueERaiz()
    {
        AtoNormativo retificado = RetificadoComVinculo();
        _atos.ObterRaizDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado.Id);
        return retificado;
    }

    private void SemConflitoDeNumero() =>
        _atos.ListarIdsComMesmaNumeracaoAsync(
            Arg.Any<NumeracaoDoAto>(), Arg.Any<CancellationToken>())
            .Returns([]);

    private void SemConflitoNoObjeto() =>
        _atos.ObterAtoConflitanteNoObjetoAsync(
            Arg.Any<EntidadeVinculada>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((AtoNormativo?)null);

    private void ConflitoNoObjetoCom(AtoNormativo ato) =>
        _atos.ObterAtoConflitanteNoObjetoAsync(
                new EntidadeVinculada(Objeto, Processo), Tipo, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(ato);

    private void VagaLivre() =>
        _atos.ObterVagaDoObjetoAsync(
            Arg.Any<EntidadeVinculada>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((VagaDeLinhagem?)null);

    private static AtoNormativo AtoDeOutraLinhagem() =>
        (AtoDoObjeto with { Numero = "99" }).Registrar();

    private static AtoNormativo AtoRetificado() => AtoDoObjeto.Registrar();

    private static RegistrarAtoNormativoCommand ComandoRetificando(AtoNormativo retificado) =>
        Comando(vinculos: null, atoRetificadoId: retificado.Id, motivo: "corrige o anexo");

    private static RegistrarAtoNormativoCommand Comando(
        IReadOnlyList<VinculoEntidadeInput>? vinculos,
        Guid? atoRetificadoId = null,
        string? motivo = null) =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: Tipo,
            DataDoAto: DataDoAto,
            DocumentoHash: HashValido,
            Assinante: PersonasDeTeste.NomeKevinCarvalho,
            AtoRetificadoId: atoRetificadoId,
            MotivoRetificacao: motivo,
            Vinculos: vinculos);
}
