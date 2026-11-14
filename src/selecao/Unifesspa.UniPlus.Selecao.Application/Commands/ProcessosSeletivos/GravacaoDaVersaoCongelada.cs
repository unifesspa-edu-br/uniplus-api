namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using Kernel.Results;

using Unifesspa.UniPlus.Application.Abstractions.Interfaces;

public sealed class GravacaoDaVersaoCongelada(
    IProcessoSeletivoRepository processos,
    IAtoDoProcessoRepository atos,
    IRascunhoDePublicacaoRepository rascunhos,
    GravacaoDaDivulgacao divulgacao,
    IUnitOfWork unitOfWork)
{
    private const string IndiceDoIdentificadorLegivelDivulgado = "ux_certames_divulgados_identificador_legivel";

    /// <summary>
    /// Grava a versão, o registro do ato, a divulgação do certame e o rascunho de publicação na mesma transação e
    /// devolve as mensagens que saem dela como cascading messages (ADR-0108): a requisição de registro do ato e a
    /// cópia dos documentos ao acervo. O Wolverine as instala no outbox dentro da transação que gravou a versão, e ou
    /// tudo existe, ou nada. O ato ainda não existe em Publicações, e não precisa: o id é decidido pelo agregado, e a
    /// versão o referencia por valor (ADR-0061). A recusa do registro do ato vem antes de qualquer escrita (ADR-0125).
    /// </summary>
    internal async Task<(Result Resposta, IEnumerable<object> Eventos)> GravarAsync(
        ProcessoSeletivo processo,
        VersaoConfiguracao versao,
        AtoEDocumentoConferidos ato,
        DadosEdital dados,
        RetificacaoInfo? retificacao,
        CancellationToken cancellationToken)
    {
        Result<AtoDoProcesso> registroDoAto = RegistroDoAtoDaVersao.Montar(
            processo, versao, ato.Ato, ato.TipoDoAto, dados, ato.Documento.Id, retificacao?.EditalRetificadoId, retificacao?.Motivo);
        if (registroDoAto.IsFailure)
        {
            return (Result.ValidationFailure(registroDoAto.Errors), []);
        }

        Result gravada = await GravarEApagarORascunhoAsync(processo, versao, registroDoAto.Value!, cancellationToken).ConfigureAwait(false);
        return gravada.IsFailure
            ? (gravada, [])
            : (Result.Success(), MensagensDaPublicacao.Montar(
                processo, versao, ato.Ato, ato.TipoDoAto, dados.Numero, ato.Documento.HashSha256!, retificacao?.EditalRetificadoId, retificacao?.Motivo));
    }

    // Só com a gravação feita o rascunho perde a razão de existir: apagar antes do flush destruiria a transcrição do
    // operador numa recusa posterior, e o apagamento é um comando direto ao banco que não pode preceder a gravação. Todo
    // caminho que registra ato apaga, porque a raiz é soft-deletable e o cascade da chave estrangeira jamais dispara.
    private async Task<Result> GravarEApagarORascunhoAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoDoProcesso registroDoAto, CancellationToken cancellationToken)
    {
        Result salva = await GravarAsync(processo, versao, registroDoAto, cancellationToken).ConfigureAwait(false);
        if (salva.IsSuccess)
        {
            await rascunhos.ApagarDoProcessoAsync(processo.Id, cancellationToken).ConfigureAwait(false);
        }

        return salva;
    }

    // A versão, o registro do ato e a divulgação vão na mesma gravação, nesta ordem: a consulta dos atos do processo lê
    // só o registro, e o certame passa a existir para o público no mesmo commit em que o ato.
    private async Task<Result> GravarAsync(
        ProcessoSeletivo processo, VersaoConfiguracao versao, AtoDoProcesso registroDoAto, CancellationToken cancellationToken)
    {
        await processos.AdicionarVersaoConfiguracaoAsync(versao, cancellationToken).ConfigureAwait(false);
        await atos.AdicionarAsync(registroDoAto, cancellationToken).ConfigureAwait(false);

        Result montada = await divulgacao.MontarDaVersaoAsync(processo, versao, registroDoAto, cancellationToken).ConfigureAwait(false);

        return montada.IsFailure ? montada : await SalvarAsync(processo, cancellationToken).ConfigureAwait(false);
    }

    // Duas transições concorrentes do mesmo processo derivam o mesmo número de versão, e o índice único e o gatilho de
    // sucessão deixam passar uma só (ADR-0102): é aqui que a linearidade da cadeia é garantida. O identificador legível
    // que outro certame já divulga é o outro conflito que só a gravação enxerga. A recusa não devolve exceção, e o
    // orquestrador descarta o rastreamento antes de o Wolverine salvar de novo (ADR-0119).
    private async Task<Result> SalvarAsync(ProcessoSeletivo processo, CancellationToken cancellationToken)
    {
        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (UniqueConstraintViolation.GetViolatedConstraint(ex) is { } constraint
            && Traduzir(constraint, processo) is { } erro)
        {
            return Result.Failure(erro);
        }

        return Result.Success();
    }

    private static DomainError? Traduzir(string constraint, ProcessoSeletivo processo) =>
        string.Equals(constraint, IndiceDoIdentificadorLegivelDivulgado, StringComparison.Ordinal)
            ? CriarProcessoSeletivoCommandHandler.IdentificadorLegivelEmUso(processo.IdentificadorLegivel!.Value)
            : VersaoConfiguracaoConstraintViolation.Traduzir(constraint);
}
