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

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RegistrarAtoNormativoCommandHandlerTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private static readonly DateOnly Publicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IPublicacoesUnitOfWork _unitOfWork = Substitute.For<IPublicacoesUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(Agora);

    [Fact(DisplayName = "Recusa quando não há tipo vigente na data de publicação")]
    public async Task Handle_SemTipoVigente_Falha()
    {
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", Publicacao, Arg.Any<CancellationToken>())
            .Returns((TipoAtoPublicado?)null);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.TipoSemVersaoVigente);
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Copia congela/efeito do catálogo vigente por valor e registra")]
    public async Task Handle_ComTipoVigente_CopiaERegistra()
    {
        VigenteComConsequencia(congela: true, efeito: true);
        SemConflitoDeNumero();

        AtoNormativo? capturado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => capturado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        capturado.Should().NotBeNull();
        capturado!.CongelaConfiguracao.Should().BeTrue();
        capturado.EfeitoIrreversivel.Should().BeTrue();
        capturado.RegistradoEm.Should().Be(Agora);
        resultado.Value!.RegistradoEm.Should().Be(Agora);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par de versão incompleto (só id) é recusado")]
    public async Task Handle_ComVersaoInvocadaIncompleta_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { VersaoInvocadaId = Guid.CreateVersion7(), VersaoInvocadaHash = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.VersaoInvocadaIncompleta);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Número já usado gera aviso, sem impedir o registro")]
    public async Task Handle_ComNumeroDuplicado_AvisaERegistra()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        Guid outro = Guid.CreateVersion7();
        _atos.ListarIdsComMesmaNumeracaoAsync(
            "CEPS", "EDITAL", 2026, "13", null, Arg.Any<CancellationToken>())
            .Returns([outro]);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().ContainSingle();
        resultado.Value.Avisos[0].Codigo.Should().Be("NumeroDuplicado");
        resultado.Value.Avisos[0].AtosConflitantes.Should().ContainSingle().Which.Should().Be(outro);
        await _atos.Received(1).AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Número único não gera aviso")]
    public async Task Handle_ComNumeroUnico_SemAviso()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Retificar ato inexistente é recusado (AtoRetificadoNaoEncontrado)")]
    public async Task Handle_RetificaAtoInexistente_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        Guid retificadoId = Guid.CreateVersion7();
        _atos.ObterPorIdParaLeituraAsync(retificadoId, Arg.Any<CancellationToken>())
            .Returns((AtoNormativo?)null);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = retificadoId, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.AtoRetificadoNaoEncontrado);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retificar com classe de congelamento divergente é recusado")]
    public async Task Handle_RetificaCongelaDivergente_Falha()
    {
        // Novo ato: tipo não congelante. Retificado: congelante. Classes divergem.
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: true);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Tipo diferente com a mesma classe de congelamento pode retificar, e a retificação não muda o tipo")]
    public async Task Handle_RetificaMesmaClasse_Registra()
    {
        // Novo ato tipo EDITAL_ABERTURA congelante; retificado tipo AVISO congelante —
        // rótulos distintos, mesma classe de congelamento (um aviso retifica um edital).
        VigenteComConsequencia(congela: true, efeito: false);
        SemConflitoDeNumero();
        AtoNormativo retificado = AtoExistente(congela: true, tipoCodigo: "AVISO");
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);

        AtoNormativo? capturado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => capturado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsSuccess.Should().BeTrue();
        capturado!.AtoRetificadoId.Should().Be(retificado.Id);
        capturado.MotivoRetificacao.Should().Be("corrige o anexo II");
        // A retificação não muda o tipo: o novo ato mantém o próprio tipo
        // (EDITAL_ABERTURA), não herda o do retificado (AVISO).
        retificado.TipoCodigo.Should().Be("AVISO");
        capturado.TipoCodigo.Should().Be("EDITAL_ABERTURA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retificar a raiz de cadeia já retificada é recusado, nomeando o retificador")]
    public async Task Handle_RetificaRaizJaRetificada_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: false);
        AtoNormativo jaRetificador = AtoExistente(congela: false);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(jaRetificador);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.RaizJaRetificada);
        resultado.Error.Message.Should().Contain(jaRetificador.Id.ToString());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Segunda retificação empilha na cabeça (retifica R1, ainda não retificado) e é aceita")]
    public async Task Handle_RetificaCabecaDaCadeia_Registra()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        AtoNormativo cabeca = AtoExistente(congela: false);
        _atos.ObterPorIdParaLeituraAsync(cabeca.Id, Arg.Any<CancellationToken>()).Returns(cabeca);
        _atos.ObterRetificadorAsync(cabeca.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = cabeca.Id, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Copia unico_por_objeto do catálogo vigente por valor")]
    public async Task Handle_CopiaUnicoPorObjeto()
    {
        VigenteComConsequencia(congela: false, efeito: false, unico: true);
        SemConflitoDeNumero();
        AtoNormativo? capturado = null;
        await _atos.AdicionarAsync(Arg.Do<AtoNormativo>(a => capturado = a), Arg.Any<CancellationToken>());

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        capturado!.UnicoPorObjeto.Should().BeTrue();
    }

    [Fact(DisplayName = "Retificação exclui o ato retificado do aviso de numeração (mesma linhagem, não colisão)")]
    public async Task Handle_RetificacaoExcluiRetificadoDoAviso()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: false);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns((AtoNormativo?)null);
        // A cadeia do retificado (aqui, só ele — é a raiz) é excluída do aviso.
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([retificado.Id]);
        // O retificado compartilha a numeração (republicação com o mesmo número); é o
        // único conflitante — deve ser excluído, resultando em nenhum aviso.
        _atos.ListarIdsComMesmaNumeracaoAsync("CEPS", "EDITAL", 2026, "13", null, Arg.Any<CancellationToken>())
            .Returns([retificado.Id]);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo II" });

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().BeEmpty();
    }

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, _tipos, _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void VigenteComConsequencia(bool congela, bool efeito, bool unico = false)
    {
        TipoAtoPublicado tipo = TipoAtoPublicado.Criar(
            "EDITAL_ABERTURA", "Edital de abertura", congela, unicoPorObjeto: unico,
            efeito, new DateOnly(2026, 1, 1), null, null).Value!;
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", Publicacao, Arg.Any<CancellationToken>())
            .Returns(tipo);
    }

    private static AtoNormativo AtoExistente(bool congela, string tipoCodigo = "EDITAL_ABERTURA") =>
        AtoNormativo.Registrar(
            Guid.CreateVersion7(),
            "CEPS", "EDITAL", 2026, "13", tipoCodigo,
            congelaConfiguracao: congela, efeitoIrreversivel: false, unicoPorObjeto: false,
            dataPublicacao: Publicacao,
            documentoHash: HashValido,
            assinante: "Jairo Belchior",
            registradoEm: Agora,
            versaoInvocada: null);

    private void SemConflitoDeNumero() =>
        _atos.ListarIdsComMesmaNumeracaoAsync(
            Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>(), Arg.Any<string>(),
            Arg.Any<Guid?>(), Arg.Any<CancellationToken>())
            .Returns([]);

    private static RegistrarAtoNormativoCommand Comando() =>
        new(
            Orgao: "CEPS",
            Serie: "EDITAL",
            Ano: 2026,
            Numero: "13",
            TipoCodigo: "EDITAL_ABERTURA",
            DataPublicacao: Publicacao,
            DocumentoHash: HashValido,
            Assinante: "Jairo Belchior");
}
