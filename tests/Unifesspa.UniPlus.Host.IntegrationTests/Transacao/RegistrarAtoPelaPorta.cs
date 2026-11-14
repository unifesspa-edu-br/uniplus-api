namespace Unifesspa.UniPlus.Host.IntegrationTests.Transacao;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;
using Unifesspa.UniPlus.Application.Abstractions.Messaging;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Comando de teste que grava uma entidade da Seleção e registra um ato em Publicações pela porta de escrita, na mesma
/// operação. <see cref="AtoRetificadoId"/> faz do ato a retificação de outro.
/// </summary>
public sealed record RegistrarAtoPelaPorta(
    string Codigo,
    Guid AtoId,
    Guid ObjetoId,
    string TipoCodigo,
    bool UnicoPorObjeto,
    DesfechoDaGravacao Desfecho,
    Guid? AtoRetificadoId = null) : ICommand<Result>;

/// <summary>
/// Handler de teste que segue o padrão do produto: grava no módulo próprio, chama a porta de escrita, faz a gravação única
/// e, na recusa ou no conflito que só a gravação enxerga, descarta o que rastreou e devolve a recusa sem evento.
/// </summary>
public static class RegistrarAtoPelaPortaHandler
{
    public static async Task<(Result, IEnumerable<object>)> Handle(
        RegistrarAtoPelaPorta comando,
        IMotivoDeDecisaoRepository motivos,
        IRegistroDeAtoNormativo registro,
        IUnitOfWork unidade,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(comando);
        ArgumentNullException.ThrowIfNull(motivos);
        ArgumentNullException.ThrowIfNull(registro);
        ArgumentNullException.ThrowIfNull(unidade);

        await motivos.AdicionarAsync(GravarEmDoisModulosHandler.NovoMotivo(comando.Codigo), cancellationToken);
        Result gravado = await RegistroEmTransacaoDeTeste.RegistrarESalvarAsync(registro, unidade, AtoDe(comando), cancellationToken);

        return gravado.IsFailure ? (gravado, []) : Concluir(comando);
    }

    private static (Result, IEnumerable<object>) Concluir(RegistrarAtoPelaPorta comando) =>
        comando.Desfecho == DesfechoDaGravacao.ExcecaoDepoisDasGravacoes
            ? throw new InvalidOperationException("Falha depois de gravar a Seleção e o ato.")
            : (Result.Success(), [new GravacaoEmDoisModulosConcluida(comando.Codigo)]);

    internal static AtoNormativoPublicado AtoDe(RegistrarAtoPelaPorta comando) =>
        AtoSemCorrecao(comando) with
        {
            Correcao = comando.AtoRetificadoId is { } retificado ? new CorrecaoDoAto(retificado, "Correção de teste") : null,
        };

    private static AtoNormativoPublicado AtoSemCorrecao(RegistrarAtoPelaPorta comando) =>
        new()
        {
            AtoId = comando.AtoId,
            Orgao = "CEPS",
            Serie = "TESTE",
            Ano = 2026,
            Numero = $"{BitConverter.ToUInt32(comando.AtoId.ToByteArray(), 12) % 900000 + 100000}/2026",
            TipoCodigo = comando.TipoCodigo,
            TipoConferido = new AtributosDoTipoAto(false, comando.UnicoPorObjeto, false, "Tipo de ato de teste"),
            DataDoAto = new DateOnly(2026, 11, 14),
            PublicadoEm = new DateTimeOffset(2026, 11, 14, 15, 0, 0, TimeSpan.Zero),
            DocumentoHash = new string('a', 64),
            Assinante = "Diretor do CEPS",
            Vinculos = [new VinculoDoAtoPublicado(EntidadesDeRegistroPeloDominio.ProcessoSeletivo, comando.ObjetoId)],
        };
}

/// <summary>
/// O padrão do produto para o fim de um comando que registra ato: a porta confere e rastreia, a gravação é única, e a recusa
/// da porta ou o conflito que só a gravação enxerga descarta o que foi rastreado e volta como recusa, sem evento.
/// </summary>
internal static class RegistroEmTransacaoDeTeste
{
    internal static async Task<Result> RegistrarESalvarAsync(
        IRegistroDeAtoNormativo registro, IUnitOfWork unidade, AtoNormativoPublicado ato, CancellationToken cancellationToken)
    {
        Result registrado = await registro.RegistrarAsync(ato, cancellationToken);
        if (registrado.IsFailure)
        {
            unidade.DescartarAlteracoesNaoSalvas();
            return registrado;
        }

        return await SalvarAsync(registro, unidade, cancellationToken);
    }

    private static async Task<Result> SalvarAsync(IRegistroDeAtoNormativo registro, IUnitOfWork unidade, CancellationToken cancellationToken)
    {
        try
        {
            await unidade.SalvarAlteracoesAsync(cancellationToken);
            return Result.Success();
        }
        catch (Exception ex) when (registro.TraduzirConflitoDaGravacao(ex) is { } recusa)
        {
            unidade.DescartarAlteracoesNaoSalvas();
            return Result.Failure(recusa);
        }
    }
}
