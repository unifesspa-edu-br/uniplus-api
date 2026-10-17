namespace Unifesspa.UniPlus.Selecao.Application.Queries.AtosDoProcesso;

using Microsoft.Extensions.Options;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.DTOs;
using Unifesspa.UniPlus.Selecao.Application.Mappings;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Interfaces;

/// <summary>
/// Lista os atos do processo: os que criaram versão do edital e os registros dos que não criam,
/// ordenados do mais recente para o mais antigo, com a situação e os documentos.
/// </summary>
/// <remarks>
/// <para>
/// O ato que criou uma versão está confirmado quando a divulgação do certame registrou o seu edital;
/// o registro do ato que não cria versão, quando o Publicações o confirmou. Os dois entram na mesma
/// lista porque o administrador pensa em publicações, não em duas tabelas.
/// </para>
/// <para>
/// A pendência acima do limiar é destacada, e só isso: a recusa do registro não é respondida, então a
/// consulta é a única forma de a pendência chegar a quem possa resolvê-la.
/// </para>
/// </remarks>
public static class ListarAtosDoProcessoQueryHandler
{
    public static async Task<Result<ListarAtosDoProcessoResult>> Handle(
        ListarAtosDoProcessoQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        IAtoDoProcessoRepository atoDoProcessoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IOptions<AtosDoProcessoOptions> opcoes,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(atoDoProcessoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(opcoes);
        ArgumentNullException.ThrowIfNull(timeProvider);

        bool existe = await processoSeletivoRepository
            .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        if (!existe)
        {
            return Result<ListarAtosDoProcessoResult>.Failure(new DomainError(
                "ProcessoSeletivo.NaoEncontrado",
                $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        IReadOnlyList<ResumoDaVersao> versoes = await processoSeletivoRepository
            .ListarResumoDasVersoesAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<AtoDoProcesso> registros = await atoDoProcessoRepository
            .ListarDoProcessoAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<DocumentoAPublicar> documentos = await documentoAPublicarRepository
            .ListarPorProcessoAsync(query.ProcessoSeletivoId, finalidade: null, cancellationToken)
            .ConfigureAwait(false);
        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        DateTimeOffset limite = timeProvider.GetUtcNow() - opcoes.Value.LimiarDePendencia;
        Dictionary<Guid, DocumentoAPublicar> documentosPorId = documentos.ToDictionary(static d => d.Id);

        List<AtoDoProcessoDto> atos = [];
        atos.AddRange(registros.Select(r => AtoDoProcessoMapping.DoRegistro(
            r,
            documentosPorId,
            EstaPendenteAlemDoLimite(r.Situacao, r.CreatedAt, limite))));

        foreach (ResumoDaVersao versao in versoes)
        {
            bool divulgada = divulgado?.EditaisDivulgados.Any(e => e.AtoId == versao.AtoCriadorId) == true;
            SituacaoDoRegistroDoAto situacao = divulgada ? SituacaoDoRegistroDoAto.Registrado : SituacaoDoRegistroDoAto.Pendente;

            // O documento da versão é o que o ato criador declarou por hash: o envelope congelado, que
            // o nomeia por id, não é lido aqui. A versão retificada pode reaproveitar o documento, e o
            // mesmo conteúdo enviado duas vezes tem dois ids — vale o primeiro a ser confirmado.
            DocumentoAPublicar? documentoDoEdital = documentos
                .Where(d => d.Status == StatusArquivoEnviado.Confirmado
                    && string.Equals(d.HashSha256, versao.AtoCriadorHash, StringComparison.Ordinal))
                .MinBy(static d => d.ConfirmadoEm);

            atos.Add(AtoDoProcessoMapping.DaVersao(
                versao,
                situacao,
                documentoDoEdital,
                EstaPendenteAlemDoLimite(situacao, versao.VigenteAPartirDe, limite)));
        }

        AtoDoProcessoDto[] ordenados =
        [
            .. atos.OrderByDescending(static a => a.PublicadoEm).ThenByDescending(static a => a.Id),
        ];

        return Result<ListarAtosDoProcessoResult>.Success(new ListarAtosDoProcessoResult(ordenados));
    }

    private static bool EstaPendenteAlemDoLimite(SituacaoDoRegistroDoAto situacao, DateTimeOffset publicadoEm, DateTimeOffset limite) =>
        situacao == SituacaoDoRegistroDoAto.Pendente && publicadoEm < limite;
}
