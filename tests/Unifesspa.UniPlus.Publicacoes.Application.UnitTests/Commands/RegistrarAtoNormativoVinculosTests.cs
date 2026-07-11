namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

using AwesomeAssertions;

using NSubstitute;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Application.Abstractions;
using Unifesspa.UniPlus.Publicacoes.Application.Commands.AtosNormativos;
using Unifesspa.UniPlus.Publicacoes.Domain.Entities;
using Unifesspa.UniPlus.Publicacoes.Domain.Errors;
using Unifesspa.UniPlus.Publicacoes.Domain.Interfaces;

/// <summary>
/// O vínculo genérico no registro do ato (ADR-0105) e a vaga que a linhagem reserva
/// (ADR-0107), do ponto de vista do handler: a herança de vínculos pela retificação, a
/// reserva da vaga, e a recusa quando o objeto já é tratado por outra linhagem.
/// </summary>
[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RegistrarAtoNormativoVinculosTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string Tipo = "EDITAL_ABERTURA";
    private const string ProcessoSeletivo = "PROCESSO_SELETIVO";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);
    private static readonly Guid Processo = Guid.CreateVersion7();

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(Agora);

    [Fact(DisplayName = "Registra o ato com os vínculos declarados")]
    public async Task Handle_ComVinculos_Registra()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        AtoNormativo? registrado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => registrado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando(vinculos: [new(ProcessoSeletivo, Processo)]));

        resultado.IsSuccess.Should().BeTrue();
        registrado!.Vinculos.Should().ContainSingle()
            .Which.EntidadeId.Should().Be(Processo);
    }

    [Fact(DisplayName = "Tipo não único por objeto não reserva vaga alguma")]
    public async Task Handle_TipoNaoUnico_NaoReservaVaga()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        await Executar(Comando(vinculos: [new(ProcessoSeletivo, Processo)]));

        await _atos.DidNotReceive().AdicionarLinhagemAsync(
            Arg.Any<LinhagemUnicaPorObjeto>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo único por objeto reserva a vaga em nome da própria raiz")]
    public async Task Handle_TipoUnico_ReservaVaga()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();
        VagaLivre();

        AtoNormativo? registrado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => registrado = a), Arg.Any<CancellationToken>());

        LinhagemUnicaPorObjeto? vaga = null;
        await _atos.AdicionarLinhagemAsync(
            Arg.Do<LinhagemUnicaPorObjeto>(l => vaga = l), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: [new(ProcessoSeletivo, Processo)]));

        resultado.IsSuccess.Should().BeTrue();
        vaga.Should().NotBeNull();
        vaga!.RaizId.Should().Be(registrado!.Id, "o ato não emenda ninguém: a linhagem nasce nele");
        vaga.EntidadeId.Should().Be(Processo);
        vaga.TipoCodigo.Should().Be(Tipo);
    }

    [Fact(DisplayName = "Tipo único por objeto sem vínculo não reserva vaga — sem objeto, não há vaga")]
    public async Task Handle_TipoUnicoSemVinculo_NaoReservaVaga()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando(vinculos: null));

        resultado.IsSuccess.Should().BeTrue();
        await _atos.DidNotReceive().AdicionarLinhagemAsync(
            Arg.Any<LinhagemUnicaPorObjeto>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Recusa com 409 quando outra linhagem já trata o objeto com um ato do mesmo tipo")]
    public async Task Handle_ObjetoTratadoPorOutraLinhagem_Falha()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        VagaLivre();

        AtoNormativo deOutraLinhagem = AtoDeOutraLinhagem();
        _atos.ObterAtoConflitanteNoObjetoAsync(
                ProcessoSeletivo, Processo, Tipo, Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns(deOutraLinhagem);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: [new(ProcessoSeletivo, Processo)]));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ObjetoJaTemAtoVivoDoTipo);
        resultado.Error.Message.Should().Contain(deOutraLinhagem.Id.ToString());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "A retificação herda os vínculos do ato que emenda")]
    public async Task Handle_Retificacao_HerdaVinculos()
    {
        Vigente(unico: false);
        SemConflitoDeNumero();

        AtoNormativo retificado = AtoRetificado();
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([retificado.Id]);
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>())
            .Returns([(ProcessoSeletivo, Processo)]);

        AtoNormativo? registrado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => registrado = a), Arg.Any<CancellationToken>());

        // A retificação não declara vínculo nenhum: sem herança, sumiria da consulta do
        // certame, e a consulta exibiria a versão superada escondendo a que a emenda.
        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: null, atoRetificadoId: retificado.Id, motivo: "corrige o anexo"));

        resultado.IsSuccess.Should().BeTrue();
        registrado!.Vinculos.Should().ContainSingle()
            .Which.EntidadeId.Should().Be(Processo);
    }

    [Fact(DisplayName = "A retificação reserva a vaga em nome da raiz da sua linhagem, não de si mesma")]
    public async Task Handle_RetificacaoDeTipoUnico_ReservaVagaEmNomeDaRaiz()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();
        VagaLivre();

        AtoNormativo retificado = AtoRetificado();
        Guid raiz = Guid.CreateVersion7();
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([raiz, retificado.Id]);
        _atos.ObterRaizDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(raiz);
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>())
            .Returns([(ProcessoSeletivo, Processo)]);

        LinhagemUnicaPorObjeto? vaga = null;
        await _atos.AdicionarLinhagemAsync(
            Arg.Do<LinhagemUnicaPorObjeto>(l => vaga = l), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: null, atoRetificadoId: retificado.Id, motivo: "corrige o anexo"));

        resultado.IsSuccess.Should().BeTrue();
        vaga!.RaizId.Should().Be(raiz, "a vaga é da linhagem, e a linhagem é identificada pela sua raiz");
    }

    [Fact(DisplayName = "A vaga já ocupada pela própria linhagem não é reservada de novo")]
    public async Task Handle_VagaDaPropriaLinhagem_NaoReservaDeNovo()
    {
        Vigente(unico: true);
        SemConflitoDeNumero();
        SemConflitoNoObjeto();

        AtoNormativo retificado = AtoRetificado();
        Guid raiz = retificado.Id;
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([raiz]);
        _atos.ObterRaizDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(raiz);
        _atos.ListarVinculosDoAtoAsync(retificado.Id, Arg.Any<CancellationToken>())
            .Returns([(ProcessoSeletivo, Processo)]);

        AtoNormativo raizDaVaga = AtoRetificado();
        VinculoAtoEntidade vinculoDaRaiz = raizDaVaga.Vinculos.First();
        _atos.ObterLinhagemDoObjetoAsync(ProcessoSeletivo, Processo, Tipo, Arg.Any<CancellationToken>())
            .Returns(LinhagemUnicaPorObjeto.Criar(raizDaVaga, vinculoDaRaiz, raiz));

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando(vinculos: null, atoRetificadoId: retificado.Id, motivo: "corrige o anexo"));

        resultado.IsSuccess.Should().BeTrue();
        await _atos.DidNotReceive().AdicionarLinhagemAsync(
            Arg.Any<LinhagemUnicaPorObjeto>(), Arg.Any<CancellationToken>());
    }

    // ── Helpers ──────────────────────────────────────────────────────────────

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, _tipos, _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void Vigente(bool unico)
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            Tipo, "Edital de abertura", congelaConfiguracao: true, unicoPorObjeto: unico,
            efeitoIrreversivel: false, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync(Tipo, Publicacao, Arg.Any<CancellationToken>()).Returns(tipo);
    }

    private void SemConflitoDeNumero() =>
        _atos.ListarIdsComMesmaNumeracaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

    private void SemConflitoNoObjeto() =>
        _atos.ObterAtoConflitanteNoObjetoAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(),
            Arg.Any<IReadOnlyCollection<Guid>>(), Arg.Any<CancellationToken>())
            .Returns((AtoNormativo?)null);

    private void VagaLivre() =>
        _atos.ObterLinhagemDoObjetoAsync(
            Arg.Any<string>(), Arg.Any<Guid>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns((LinhagemUnicaPorObjeto?)null);

    private static AtoNormativo AtoDeOutraLinhagem() =>
        AtoNormativo.Registrar(
            "CEPS", "EDITAL", 2026, "99", Tipo,
            congelaConfiguracao: true, efeitoIrreversivel: false, unicoPorObjeto: true,
            dataPublicacao: Publicacao, documentoHash: HashValido, assinante: "Jairo Belchior",
            registradoEm: Agora, versaoInvocada: null,
            vinculos: [(ProcessoSeletivo, Processo)]);

    private static AtoNormativo AtoRetificado() =>
        AtoNormativo.Registrar(
            "CEPS", "EDITAL", 2026, "13", Tipo,
            congelaConfiguracao: true, efeitoIrreversivel: false, unicoPorObjeto: true,
            dataPublicacao: Publicacao, documentoHash: HashValido, assinante: "Jairo Belchior",
            registradoEm: Agora, versaoInvocada: null,
            vinculos: [(ProcessoSeletivo, Processo)]);

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
            DataPublicacao: Publicacao,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior",
            AtoRetificadoId: atoRetificadoId,
            MotivoRetificacao: motivo,
            Vinculos: vinculos);
}
