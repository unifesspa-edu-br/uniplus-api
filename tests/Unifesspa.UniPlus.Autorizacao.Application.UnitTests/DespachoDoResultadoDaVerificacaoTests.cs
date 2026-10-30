namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Microsoft.Extensions.Logging.Abstractions;

using Unifesspa.UniPlus.Autorizacao.Application.Abstractions;
using Unifesspa.UniPlus.Autorizacao.Application.Commands;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Enums;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;
using Unifesspa.UniPlus.Autorizacao.Domain.ValueObjects;
using Unifesspa.UniPlus.Kernel.Pagination;
using Unifesspa.UniPlus.Verificador.Contratos;

/// <summary>
/// A fila de resultados da Autorização é uma só e o resultado traz só o id do arquivo: o handler procura a instalação
/// e, não a achando, o anexo de pedido. O id que não é de nenhum dos dois é descartado sem gravar.
/// </summary>
public sealed class DespachoDoResultadoDaVerificacaoTests
{
    private const string Emissor = "https://idp.test/realms/unifesspa";
    private const string Sha = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

    [Fact(DisplayName = "O resultado de um id que não é de instalação vai ao anexo do pedido e grava o SHA-256")]
    public async Task Resultado_DeAnexo_AtualizaOAnexo()
    {
        PedidoDeConcessao pedido = PedidoComAnexoEmVerificacao();
        AnexoDoPedido anexo = pedido.Anexos.Single();
        UnidadeDeTrabalhoQueConta unidade = new();

        await Despachar(ResultadoVerificado(anexo.Id), new PedidosComAnexo(anexo, pedido), unidade);

        anexo.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        anexo.Sha256.Should().Be(Sha);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O documento verificado libera o pedido que esperava a designação, na mesma gravação")]
    public async Task Resultado_DeAnexoVerificado_LiberaOPedidoQueEsperavaODocumento()
    {
        PedidoDeConcessao pedido = PedidoComAnexoEmVerificacao(PerfilDeGovernanca());
        AnexoDoPedido anexo = pedido.Anexos.Single();
        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        PedidosComAnexo pedidos = new(anexo, pedido);
        UnidadeDeTrabalhoQueConta unidade = new();

        await Despachar(ResultadoVerificado(anexo.Id), pedidos, unidade);

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoBeneficiario);
        pedidos.Atualizados.Should().ContainSingle().Which.Should().BeSameAs(pedido);
        unidade.Gravacoes.Should().Be(1);
    }

    [Fact(DisplayName = "O documento recusado mantém o pedido aguardando a designação")]
    public async Task Resultado_DeAnexoRecusado_MantemOPedidoAguardandoODocumento()
    {
        PedidoDeConcessao pedido = PedidoComAnexoEmVerificacao(PerfilDeGovernanca());
        AnexoDoPedido anexo = pedido.Anexos.Single();
        PedidosComAnexo pedidos = new(anexo, pedido);

        await Despachar(
            new ResultadoDaVerificacaoDeArquivo(anexo.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null),
            pedidos,
            new UnidadeDeTrabalhoQueConta());

        pedido.Situacao.Should().Be(SituacaoDoPedido.AguardandoDocumento);
        pedidos.Atualizados.Should().BeEmpty();
    }

    [Fact(DisplayName = "O veredicto de recusa encerra o anexo e o reenvio também")]
    public async Task Resultado_DeAnexoRecusado_EncerraOAnexo()
    {
        PedidoDeConcessao pedido = PedidoComAnexoEmVerificacao();
        AnexoDoPedido anexo = pedido.Anexos.Single();

        await Despachar(
            new ResultadoDaVerificacaoDeArquivo(anexo.Id, VeredictoDaVerificacao.ReenvioNecessario, MotivoDaRecusa.NaoSeAplica, null, null, null),
            new PedidosComAnexo(anexo, pedido),
            new UnidadeDeTrabalhoQueConta());

        anexo.Situacao.Should().Be(SituacaoDoAnexo.Recusado);
        anexo.RecusadoEm.Should().NotBeNull();
    }

    [Fact(DisplayName = "O resultado repetido de anexo que já saiu da verificação é ignorado sem gravar")]
    public async Task Resultado_RepetidoDeAnexo_Ignora()
    {
        PedidoDeConcessao pedido = PedidoComAnexoEmVerificacao();
        AnexoDoPedido anexo = pedido.Anexos.Single();
        anexo.RegistrarVerificado(Sha, DateTimeOffset.UtcNow);
        UnidadeDeTrabalhoQueConta unidade = new();

        await Despachar(
            new ResultadoDaVerificacaoDeArquivo(anexo.Id, VeredictoDaVerificacao.Recusado, MotivoDaRecusa.PdfComSenha, null, null, null),
            new PedidosComAnexo(anexo, pedido),
            unidade);

        anexo.Situacao.Should().Be(SituacaoDoAnexo.Verificado);
        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O resultado de um id que não é de instalação nem de anexo é descartado sem gravar")]
    public async Task Resultado_DeIdDesconhecido_Descarta()
    {
        UnidadeDeTrabalhoQueConta unidade = new();

        await Despachar(ResultadoVerificado(Guid.NewGuid()), new PedidosComAnexo(null), unidade);

        unidade.Gravacoes.Should().Be(0);
    }

    [Fact(DisplayName = "O id de instalação é resolvido antes de consultar o anexo")]
    public async Task Resultado_DeInstalacao_NaoConsultaOAnexo()
    {
        Instalacao instalacao = Instalacao.Iniciar(
            new Identidade(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0001"),
            [new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0002", "gestor2@uniplus-test.local"), new(Emissor, "0e6b9a40-1c11-4f8e-9d52-6a1f0b2c0003", "gestor3@uniplus-test.local")],
            Emissor, "123", 1024, DateTimeOffset.UtcNow).Value!;
        instalacao.ConfirmarDocumento("\"etag\"");

        await RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            ResultadoVerificado(instalacao.Id),
            new InstalacaoNaMemoria(instalacao),
            new PedidosQueNaoDevemSerConsultados(),
            new UnidadeDeTrabalhoQueConta(),
            TimeProvider.System,
            NullLogger.Instance,
            CancellationToken.None);

        instalacao.Situacao.Should().Be(SituacaoDaInstalacao.DocumentoVerificado);
    }

    private static Task Despachar(ResultadoDaVerificacaoDeArquivo resultado, IPedidoDeConcessaoRepository pedidos, IAutorizacaoUnitOfWork unidade) =>
        RegistrarResultadoDaVerificacaoDoDocumentoDaAutorizacaoHandler.Handle(
            resultado, new InstalacaoNaMemoria(null), pedidos, unidade, TimeProvider.System, NullLogger.Instance, CancellationToken.None);

    private static ResultadoDaVerificacaoDeArquivo ResultadoVerificado(Guid id) =>
        new(id, VeredictoDaVerificacao.Verificado, MotivoDaRecusa.NaoSeAplica, Sha, 1024, FormatoDoArquivo.Pdf);

    private static Perfil PerfilDeGovernanca()
    {
        Perfil perfil = Perfil.Criar("Gestão de acessos", "Opera as concessões de acesso.", ["autorizacao:concessoes:administrar"], new Catalogo()).Value!;
        typeof(Perfil).GetProperty(nameof(Perfil.Governanca))!.SetValue(perfil, TipoDeGovernanca.GestorDeAcessos);
        return perfil;
    }

    // Com o perfil de governança o pedido é para outra pessoa, e o desfecho do documento verificado é o aceite dela.
    private static PedidoDeConcessao PedidoComAnexoEmVerificacao(Perfil? perfilPedido = null)
    {
        Perfil perfil = perfilPedido ?? Perfil.Criar("Análise de atendimento", "Analisa os pedidos de atendimento.", ["selecao:processos:consultar"], new Catalogo()).Value!;
        PedidoDeConcessao pedido = PedidoDeConcessao.Criar(
            new PessoaNoToken(new Identidade(Emissor, "betina.neves"), "Betina Marina Valentina das Neves", "betina-dasneves81@uniplus-test.local", true),
            perfilPedido is null ? null : "leandro-viana84@uniplus-test.local",
            perfil,
            perfilPedido is null ? EscopoDaConcessao.DaUnidade(Guid.NewGuid()) : EscopoDaConcessao.DaInstituicao(),
            "A coordenação do CEPS precisa analisar os pedidos de atendimento.",
            new Dominios(),
            new Catalogo(),
            DateTimeOffset.UtcNow).Value!;
        AnexoDoPedido anexo = pedido.NovoAnexo(1024).Value!;
        pedido.ConfirmarAnexo(anexo.Id, "\"etag\"", DateTimeOffset.UtcNow).IsSuccess.Should().BeTrue();
        return pedido;
    }

    private sealed class Catalogo : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;

        public bool DeUnidade(string codigo) => codigo.StartsWith("selecao:", StringComparison.Ordinal);
    }

    private sealed class Dominios : IDominiosInstitucionais
    {
        public bool Contem(string dominio) => true;
    }

    private sealed class UnidadeDeTrabalhoQueConta : IAutorizacaoUnitOfWork
    {
        public int Gravacoes { get; private set; }

        public Task<int> SalvarAlteracoesAsync(CancellationToken cancellationToken = default)
        {
            Gravacoes++;
            return Task.FromResult(1);
        }

        public void DescartarAlteracoesNaoSalvas()
        {
        }
    }

    private sealed class InstalacaoNaMemoria(Instalacao? instalacao) : IInstalacaoRepository
    {
        public Task<Instalacao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(instalacao is not null && instalacao.Id == id ? instalacao : null);

        public Task<IReadOnlyList<Instalacao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarAsync(Instalacao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public void Atualizar(Instalacao entity) => throw new InvalidOperationException();
        public void Remover(Instalacao entity) => throw new InvalidOperationException();
        public Task<Instalacao?> ObterPendenteDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Instalacao?> ObterAguardandoCodigoDoExecutorAsync(Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Instalacao?> ObterDoExecutorAsync(Guid id, Identidade executor, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<Instalacao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task AdicionarTentativaAsync(TentativaDeInstalacao tentativa, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
        public Task<IReadOnlyList<TentativaDeInstalacao>> ListarTentativasAsync(Guid instalacaoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

        public Task<(IReadOnlyList<TentativaDeInstalacao> Itens, Guid? AnteriorAfterId, Guid? ProximoAfterId)> ListarTentativasSemPendenciaAsync(
            Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    }

    private sealed class PedidosQueNaoDevemSerConsultados : PedidosComAnexo
    {
        public PedidosQueNaoDevemSerConsultados()
            : base(null)
        {
        }

        public override Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) =>
            throw new InvalidOperationException();
    }
}

/// <summary>Repositório de pedidos que só sabe devolver o anexo pelo id.</summary>
internal class PedidosComAnexo(AnexoDoPedido? anexo, PedidoDeConcessao? pedido = null) : IPedidoDeConcessaoRepository
{
    public List<PedidoDeConcessao> Atualizados { get; } = [];

    public virtual Task<AnexoDoPedido?> ObterAnexoAsync(Guid anexoId, CancellationToken cancellationToken = default) =>
        Task.FromResult(anexo is not null && anexo.Id == anexoId ? anexo : null);

    public Task<PedidoDeConcessao?> ObterPorIdAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<PedidoDeConcessao>> ObterTodosAsync(CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task AdicionarAsync(PedidoDeConcessao entity, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public void Atualizar(PedidoDeConcessao entity) => Atualizados.Add(entity);
    public void Remover(PedidoDeConcessao entity) => throw new InvalidOperationException();
    public Task<PedidoDeConcessao?> ObterParaAlterarAsync(Guid id, CancellationToken cancellationToken = default) =>
        Task.FromResult(pedido is not null && pedido.Id == id ? pedido : null);
    public Task<PedidoDeConcessao?> ObterParaLeituraAsync(Guid id, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task AdicionarLeituraAsync(LeituraDoAnexoDoPedido leitura, CancellationToken cancellationToken = default) => throw new InvalidOperationException();
    public Task<IReadOnlyList<LeituraDoAnexoDoPedido>> ListarLeiturasAsync(Guid pedidoId, CancellationToken cancellationToken = default) => throw new InvalidOperationException();

    public Task<(IReadOnlyList<PedidoComPerfil> Itens, (string SortKey, Guid Id)? Anterior, (string SortKey, Guid Id)? Proximo)> ListarPaginadoAsync(
        FiltroDePedidos filtro, string? afterSortKey, Guid? afterId, int limit, PaginationDirection direction, CancellationToken cancellationToken = default) =>
        throw new InvalidOperationException();
}
