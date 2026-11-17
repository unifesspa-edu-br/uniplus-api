namespace Unifesspa.UniPlus.Publicacoes.Application.UnitTests.Commands;

using System.Diagnostics.CodeAnalysis;

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
using Unifesspa.UniPlus.Testes.Compartilhado;

[SuppressMessage(
    "Performance",
    "CA1515:Consider making public types internal",
    Justification = "xUnit exige tipo de teste público.")]
public sealed class RegistrarAtoNormativoCommandHandlerTests
{
    private const string HashValido = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";
    private const string OutroHash = "fedcba9876543210fedcba9876543210fedcba9876543210fedcba9876543210";
    private static readonly DateOnly DataDoAto = new(2026, 3, 10);
    private static readonly DateOnly DiaDaPublicacao = new(2026, 3, 13);
    private static readonly DateTimeOffset Agora = new(2026, 3, 13, 19, 0, 0, TimeSpan.Zero);

    private readonly ITipoAtoPublicadoRepository _tipos = Substitute.For<ITipoAtoPublicadoRepository>();
    private readonly IAtoNormativoRepository _atos = Substitute.For<IAtoNormativoRepository>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly TimeProvider _relogio = new RelogioFixo(Agora);
    private AtoNormativo? _adicionado;

    public RegistrarAtoNormativoCommandHandlerTests() =>
        _atos.When(a => a.AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>()))
            .Do(chamada => _adicionado = chamada.Arg<AtoNormativo>());

    [Fact(DisplayName = "Recusa quando não há tipo vigente na data da publicação")]
    public async Task Handle_SemTipoVigente_Falha()
    {
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", DiaDaPublicacao, Arg.Any<CancellationToken>())
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

        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        _adicionado.Should().NotBeNull();
        _adicionado!.CongelaConfiguracao.Should().BeTrue();
        _adicionado!.EfeitoIrreversivel.Should().BeTrue();
        _adicionado!.PublicadoEm.Should().Be(Agora);
        resultado.Value!.PublicadoEm.Should().Be(Agora);
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Par de versão incompleto (só id) é recusado")]
    public async Task Handle_ComVersaoDoObjetoIncompleta_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { VersaoDoObjetoId = Guid.CreateVersion7(), VersaoDoObjetoHash = null });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.VersaoDoObjetoIncompleta);
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

    /// <remarks>O novo ato é de tipo não congelante e o retificado é congelante: as classes divergem.</remarks>
    [Fact(DisplayName = "Retificar com classe de congelamento divergente é recusado")]
    public async Task Handle_RetificaCongelaDivergente_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: true);
        _atos.ComoRetificavel(retificado);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoQueRetifica(retificado));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.ClasseDeCongelamentoDivergente);
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    /// <remarks>
    /// O novo ato é do tipo EDITAL_ABERTURA e o retificado, do tipo AVISO, ambos congelantes: rótulos
    /// distintos, mesma classe de congelamento (um aviso retifica um edital). A retificação não muda o
    /// tipo: o novo ato mantém o próprio, não herda o do retificado.
    /// </remarks>
    [Fact(DisplayName = "Tipo diferente com a mesma classe de congelamento pode retificar, e a retificação não muda o tipo")]
    public async Task Handle_RetificaMesmaClasse_Registra()
    {
        VigenteComConsequencia(congela: true, efeito: false);
        SemConflitoDeNumero();
        AtoNormativo retificado = AtoExistente(congela: true, tipoCodigo: "AVISO");
        _atos.ComoRetificavel(retificado);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoQueRetifica(retificado));

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.AtoRetificadoId.Should().Be(retificado.Id);
        _adicionado!.MotivoRetificacao.Should().Be("corrige o anexo II");
        retificado.TipoCodigo.Should().Be("AVISO");
        _adicionado!.TipoCodigo.Should().Be("EDITAL_ABERTURA");
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Retificar um ato já retificado é recusado, nomeando o retificador")]
    public async Task Handle_RetificaAtoJaRetificado_Falha()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: false);
        AtoNormativo jaRetificador = AtoExistente(congela: false);
        _atos.ObterPorIdParaLeituraAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(retificado);
        _atos.ObterRetificadorAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns(jaRetificador);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoQueRetifica(retificado));

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AtoNormativoErrorCodes.AtoJaRetificado);
        resultado.Error.Message.Should().Contain("EDITAL nº 13/2026").And.NotContain(jaRetificador.Id.ToString());
        await _unitOfWork.DidNotReceive().SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Segunda retificação empilha na cabeça (retifica R1, ainda não retificado) e é aceita")]
    public async Task Handle_RetificaCabecaDaCadeia_Registra()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        AtoNormativo cabeca = AtoExistente(congela: false);
        _atos.ComoRetificavel(cabeca);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoQueRetifica(cabeca));

        resultado.IsSuccess.Should().BeTrue();
        await _unitOfWork.Received(1).SalvarAlteracoesAsync(Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Copia unico_por_objeto do catálogo vigente por valor")]
    public async Task Handle_CopiaUnicoPorObjeto()
    {
        VigenteComConsequencia(congela: false, efeito: false, unico: true);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.UnicoPorObjeto.Should().BeTrue();
    }

    [Fact(DisplayName = "Registro pelo endpoint copia o nome do tipo vigente por valor")]
    public async Task Handle_PeloEndpoint_CopiaNomeDoTipoVigente()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando());

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.TipoNome.Should().Be("Edital de abertura");
    }

    /// <remarks>O catálogo já tem outro nome para o tipo: se o handler o relesse, gravaria este.</remarks>
    [Fact(DisplayName = "Registro por mensagem grava o nome conferido pelo emissor, sem reler o catálogo")]
    public async Task Handle_PorMensagem_GravaNomeConferidoSemReler()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoPorMensagem(nome: "Edital de abertura (nome da publicação)"));

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.TipoNome.Should().Be("Edital de abertura (nome da publicação)");
        await _tipos.DidNotReceive().ObterVigenteAsync(
            Arg.Any<string>(), Arg.Any<DateOnly>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Requisição emitida sem o nome registra o ato sem nome, com o código presente")]
    public async Task Handle_PorMensagemSemNome_RegistraSemNome()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(ComandoPorMensagem(nome: null));

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.TipoNome.Should().BeNull();
        _adicionado!.TipoCodigo.Should().Be("EDITAL_ABERTURA");
    }

    /// <remarks>
    /// A cadeia do retificado (aqui, só ele, que é a raiz) é excluída do aviso. O retificado compartilha
    /// a numeração, como numa republicação com o mesmo número, e é o único conflitante: excluído, não
    /// sobra aviso nenhum.
    /// </remarks>
    [Fact(DisplayName = "Retificação exclui o ato retificado do aviso de numeração (mesma linhagem, não colisão)")]
    public async Task Handle_RetificacaoExcluiRetificadoDoAviso()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        AtoNormativo retificado = AtoExistente(congela: false);
        _atos.ComoRetificavel(retificado);
        _atos.ListarIdsDaCadeiaAsync(retificado.Id, Arg.Any<CancellationToken>()).Returns([retificado.Id]);
        _atos.ListarIdsComMesmaNumeracaoAsync("CEPS", "EDITAL", 2026, "13", null, Arg.Any<CancellationToken>())
            .Returns([retificado.Id]);

        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            ComandoQueRetifica(retificado));

        resultado.IsSuccess.Should().BeTrue();
        resultado.Value!.Avisos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Grava os anexos na ordem enviada")]
    public async Task Handle_ComAnexos_GravaNaOrdemEnviada()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { Anexos = [AnexoPdf("Anexo II.pdf", OutroHash), AnexoPdf("Anexo I.pdf", HashValido)] });

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.Anexos.OrderBy(a => a.Ordem).Select(a => a.Nome)
            .Should().Equal("Anexo II.pdf", "Anexo I.pdf");
    }

    [Fact(DisplayName = "Sem a lista de anexos (mensagem antiga), registra o ato com lista vazia")]
    public async Task Handle_SemAnexos_RegistraComListaVazia()
    {
        VigenteComConsequencia(congela: false, efeito: false);
        SemConflitoDeNumero();
        Result<RegistrarAtoNormativoResult> resultado = await Executar(Comando() with { Anexos = null });

        resultado.IsSuccess.Should().BeTrue();
        _adicionado!.Anexos.Should().BeEmpty();
    }

    [Fact(DisplayName = "Anexo com nome repetido é recusado antes de qualquer ida ao banco")]
    public async Task Handle_AnexoComNomeDuplicado_RecusaSemTocarOBanco()
    {
        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { Anexos = [AnexoPdf("Anexo I.pdf", HashValido), AnexoPdf("Anexo I.pdf", OutroHash)] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AnexoDoAtoErrorCodes.NomeDuplicado);
        await _tipos.DidNotReceiveWithAnyArgs().ObterVigenteAsync(default!, default, default);
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
    }

    [Fact(DisplayName = "Elemento nulo na lista de anexos é recusado, sem descartá-lo")]
    public async Task Handle_AnexoNulo_RecusaSemDescartar()
    {
        Result<RegistrarAtoNormativoResult> resultado = await Executar(
            Comando() with { Anexos = [AnexoPdf("Anexo I.pdf", HashValido), null!] });

        resultado.IsFailure.Should().BeTrue();
        resultado.Error!.Code.Should().Be(AnexoDoAtoErrorCodes.Nulo);
        await _atos.DidNotReceive().AdicionarAsync(Arg.Any<AtoNormativo>(), Arg.Any<CancellationToken>());
    }

    private Task<Result<RegistrarAtoNormativoResult>> Executar(RegistrarAtoNormativoCommand comando) =>
        RegistrarAtoNormativoCommandHandler.Handle(
            comando, MontagemDeTeste.Criar(_tipos, _atos), _atos, _unitOfWork, _relogio, CancellationToken.None);

    private void VigenteComConsequencia(bool congela, bool efeito, bool unico = false)
    {
        TipoAtoPublicado tipo = new TipoAtoPublicadoDeTeste
        {
            CongelaConfiguracao = congela,
            UnicoPorObjeto = unico,
            EfeitoIrreversivel = efeito,
        }.Criar().Value!;
        _tipos.ObterVigenteAsync("EDITAL_ABERTURA", DiaDaPublicacao, Arg.Any<CancellationToken>())
            .Returns(tipo);
    }

    private static AtoNormativo AtoExistente(bool congela, string tipoCodigo = "EDITAL_ABERTURA") =>
        new AtoNormativoDeTeste { CongelaConfiguracao = congela, TipoCodigo = tipoCodigo, DataDoAto = DataDoAto }.Registrar();

    private static AnexoDoAtoInput AnexoPdf(string nome, string hash) => new(nome, "application/pdf", hash);

    private static RegistrarAtoNormativoCommand ComandoQueRetifica(AtoNormativo retificado) =>
        Comando() with { AtoRetificadoId = retificado.Id, MotivoRetificacao = "corrige o anexo II" };

    private static RegistrarAtoNormativoCommand ComandoPorMensagem(string? nome) =>
        Comando() with
        {
            AtoId = Guid.CreateVersion7(),
            AtributosDoTipo = new AtributosDoTipoAto(
                CongelaConfiguracao: false, UnicoPorObjeto: false, EfeitoIrreversivel: false, Nome: nome),
        };

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
            DataDoAto: DataDoAto,
            DocumentoHash: HashValido,
            Assinante: PersonasDeTeste.NomeKevinCarvalho);
}
