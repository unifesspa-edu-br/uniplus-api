namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Interfaces;
using Unifesspa.UniPlus.Kernel.Results;
using DTOs;

/// <summary>
/// Handler convention-based do <see cref="ObterSnapshotVigenteQuery"/> (RN08,
/// ADR-0075/0076/0068/0104): resolve o instante (explícito ou default do
/// <c>TimeProvider</c>), seleciona a VERSÃO vigente da configuração e projeta o
/// snapshot congelado, hidratando os campos documentais a partir do ato que a
/// criou. Distingue 404 (processo inexistente) de 422
/// (<c>Snapshot.VigenteAusente</c>) — nunca retorno silencioso.
/// </summary>
/// <remarks>
/// A ordem das duas leituras é a decisão da ADR-0104 em código: primeiro a
/// versão — que é o que ordena a configuração —, depois o documento, e só para
/// projetar o que o contrato de leitura já publicava sobre ele. Não existe
/// caminho em que um atributo do ato decida qual configuração vale.
/// </remarks>
public static class ObterSnapshotVigenteQueryHandler
{
    public static async Task<Result<SnapshotVigenteDto>> Handle(
        ObterSnapshotVigenteQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(timeProvider);

        // ADR-0075/0068: o instante é sempre explícito no seletor; quando o
        // cliente o omite, o default vem do TimeProvider injetado — nunca de um
        // relógio lido dentro do repositório por trás do contrato.
        //
        // O "agora" é o relógio como ele está, sem correção. Se o host regredir
        // para antes da vigência da versão corrente, a consulta corrente aflora
        // Snapshot.VigenteAusente até o relógio se recuperar — e não uma versão
        // que ainda não vigora. Um "agora" artificialmente monotônico daria uma
        // configuração como vigente contra um presente que o próprio sistema não
        // reconhece, mascarando o relógio errado em vez de expô-lo (ADR-0076).
        DateTimeOffset instante = query.Instante ?? timeProvider.GetUtcNow();

        VersaoConfiguracao? versao = await processoSeletivoRepository
            .ObterVersaoVigenteAsync(query.ProcessoSeletivoId, instante, cancellationToken)
            .ConfigureAwait(false);

        if (versao is null)
        {
            // Só quando não há vigente distingue 404 de 422 — o caminho comum
            // (existe configuração vigente) não paga essa consulta.
            bool existe = await processoSeletivoRepository
                .ExisteAsync(query.ProcessoSeletivoId, cancellationToken)
                .ConfigureAwait(false);

            return existe
                ? Result<SnapshotVigenteDto>.Failure(new DomainError(
                    "Snapshot.VigenteAusente",
                    $"Nenhuma publicação vigente para o instante {instante:O}."))
                : Result<SnapshotVigenteDto>.Failure(new DomainError(
                    "ProcessoSeletivo.NaoEncontrado",
                    $"Processo Seletivo {query.ProcessoSeletivoId} não encontrado."));
        }

        DadosDocumentaisAto? ato = await processoSeletivoRepository
            .ObterDadosDocumentaisDoAtoAsync(query.ProcessoSeletivoId, versao.AtoCriadorId, cancellationToken)
            .ConfigureAwait(false);

        if (ato is null)
        {
            // Uma versão sem o ato que a criou é corrupção, não ausência: as duas
            // linhas nascem na mesma transação. Mascarar como VigenteAusente (422)
            // diria ao cliente que o certame não tem configuração vigente — quando
            // tem, e o que falta é o documento. A ausência que a ADR-0076 manda
            // aflorar é a de configuração, não a de evidência corrompida.
            throw new InvalidOperationException(
                $"A versão {versao.Id} do processo {query.ProcessoSeletivoId} referencia o ato {versao.AtoCriadorId}, que não existe neste processo.");
        }

        // O contrato de leitura não muda com a ADR-0104 (o mecanismo, sim): o
        // identificador forense devolvido continua sendo o da VERSÃO congelada — a
        // mesma referência durável que o ProcessoPublicadoEvent carrega — e o hash
        // do ato criador, o hash do documento que a congelou.
        return Result<SnapshotVigenteDto>.Success(new SnapshotVigenteDto(
            versao.Id,
            ato.DataPublicacao,
            ato.Natureza,
            versao.SchemaVersion,
            versao.AlgoritmoHash,
            versao.HashConfiguracao,
            versao.AtoCriadorHash,
            JsonNode.Parse(versao.ConfiguracaoCongelada)!));
    }
}
