namespace Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Text.Json;
using System.Text.Json.Nodes;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Queries.ProcessosSeletivos;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Publicacoes.Contracts;

/// <summary>
/// Materializa a divulgação pública do certame quando o ato normativo da sua versão se confirma no
/// registro central.
/// </summary>
/// <remarks>
/// <para>
/// É aqui que o certame passa a existir para o público: a publicação congela a configuração, mas a
/// publicidade depende de o ato existir, e isso só se sabe no consumo.
/// </para>
/// <para>
/// <b>Idempotente por desenho.</b> A reentrega é esperada, e uma entrega atrasada pode trazer o ato
/// de uma versão anterior à divulgada: a linha recusa retroceder, mas registra o edital que o ato
/// atrasado divulgou — a cópia dele já está no acervo, e a linha do tempo pública precisa do endereço.
/// </para>
/// <para>
/// A recusa de mérito não chega aqui: ela é terminal e fica na fila morta. O efeito é exatamente o
/// pretendido — a linha não avança, e o certame permanece no ar com o conteúdo da versão anterior.
/// </para>
/// <para>
/// <b>É também o único caminho de escrita no acervo público (ADR-0132).</b> O documento do edital, os
/// anexos e os modelos de documento que ele congelou são copiados para lá antes de a divulgação ser gravada,
/// porque é aqui que o registro do ato está confirmado: nada fica público antes do ato, e o link só é
/// divulgado depois de o arquivo estar no acervo. Falha na cópia lança, a divulgação não avança, e a reentrega repete —
/// encontrando no acervo o que já tinha sido copiado.
/// </para>
/// </remarks>
public static class DivulgarCertameAoRegistrarAtoHandler
{
    public static async Task Handle(
        AtoNormativoRegistrado mensagem,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        ISelecaoUnitOfWork unitOfWork,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(mensagem);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(documentoAPublicarRepository);
        ArgumentNullException.ThrowIfNull(modeloDeDocumentoRepository);
        ArgumentNullException.ThrowIfNull(acervoPublico);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);
        ArgumentNullException.ThrowIfNull(unitOfWork);
        ArgumentNullException.ThrowIfNull(timeProvider);

        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([mensagem.AtoId], cancellationToken)
            .ConfigureAwait(false);

        // Ato que não corresponde a versão alguma de Seleção: o registro central serve outros
        // domínios, e um ato de outro objeto chega aqui sem ter o que divulgar.
        if (versoes.Count == 0)
        {
            return;
        }

        VersaoConfiguracao versao = versoes[0];

        if (!registroCodecs.SabeLer(versao.SchemaVersion)
            || !TentarLerEnvelope(versao.ConfiguracaoCongelada, out JsonObject? envelope))
        {
            // O documento congelado existe mas não é legível por ESTE processo. Divulgar meia
            // projeção seria pior que não divulgar — a ausência é visível e reprojetável, a
            // projeção parcial mente.
            //
            // O tipo próprio é o que permite à política de reentrega separar este caso da falha
            // transiente: aqui a causa esperada é a janela de um deploy em fases, que dura minutos,
            // e não os segundos de um blip.
            throw new EnvelopeAindaNaoLegivelException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não pôde ser lida para divulgação (schema {versao.SchemaVersion}).");
        }

        // O título é atributo do processo, não da configuração congelada: lê-lo aqui é o que o
        // congela contra edição sob retificação sem publicidade.
        string? nome = await processoSeletivoRepository
            .ObterNomeAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (nome is null)
        {
            throw new InvalidOperationException(
                $"Processo {versao.ProcessoSeletivoId} não encontrado para divulgar o certame.");
        }

        AtoDaDivulgacao ato = new(versao.AtoCriadorId, mensagem.Ano, mensagem.TipoCodigo);
        Result<CertamePublicadoDto> projecao = ProjecaoDoCertamePublicado.Projetar(
            versao.ProcessoSeletivoId, ato, nome, versao.HashConfiguracao, envelope, enderecoNoAcervo.De);

        if (projecao.IsFailure)
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada: {projecao.Error!.Message}");
        }

        // Antes de ler a linha de divulgação para mutação: a reentrega que a cópia falhada provoca
        // começa sem nada rastreado, e a cópia já feita é encontrada no acervo e não se repete.
        DocumentoDivulgado principal = await PublicarEditalNoAcervoAsync(
            versao, ato, envelope, documentoAPublicarRepository, acervoPublico, enderecoNoAcervo, cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<DocumentoDivulgado> anexos = await PublicarAnexosNoAcervoAsync(
            versao, envelope, documentoAPublicarRepository, acervoPublico, enderecoNoAcervo, cancellationToken)
            .ConfigureAwait(false);
        await PublicarModelosNoAcervoAsync(versao, envelope, modeloDeDocumentoRepository, acervoPublico, cancellationToken)
            .ConfigureAwait(false);

        CertamePublicadoDto certame = projecao.Value!;

        // Registrado só depois da cópia: o endereço divulgado nunca aponta para arquivo ausente.
        // É o endereço que a projeção desta versão já calculou para o edital, sem segunda derivação.
        EditalDivulgado editalDivulgado = new(
            versao.AtoCriadorId, versao.NumeroVersao, certame.DocumentoEdital.UrlDownload.OriginalString, [principal, .. anexos]);
        string documento = JsonSerializer.Serialize(certame, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        DateTimeOffset agora = timeProvider.GetUtcNow();

        // Da MESMA projeção que produziu o documento: é o que impede consulta e resposta divergirem.
        FacetasDoCertameDivulgado facetas = new(
            certame.IdentificadorLegivel,
            certame.Nome,
            certame.Periodo.Numero,
            certame.ModalidadesOfertadas,
            certame.Periodo.Inicio,
            certame.Periodo.Fim);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaMutacaoAsync(versao.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (divulgado is null)
        {
            CertameDivulgado nova = CertameDivulgado.Criar(
                versao.ProcessoSeletivoId,
                versao.NumeroVersao,
                versao.AtoCriadorId,
                versao.HashConfiguracao,
                ProjecaoDoCertamePublicado.Versao,
                facetas,
                documento,
                agora);
            nova.RegistrarEdital(editalDivulgado);

            await certameDivulgadoRepository.AdicionarAsync(nova, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            bool avancou = divulgado.TentarAvancar(
                versao.NumeroVersao,
                versao.AtoCriadorId,
                versao.HashConfiguracao,
                ProjecaoDoCertamePublicado.Versao,
                facetas,
                documento,
                agora);

            // O edital registra-se também quando a versão não avança: o ato atrasado de uma versão
            // anterior não desfaz a vigente, mas o seu edital já está no acervo e a linha do tempo
            // precisa dele. Reentrega do que já está registrado não muda nada.
            bool registrou = divulgado.RegistrarEdital(editalDivulgado);

            if (!avancou && !registrou)
            {
                return;
            }
        }

        try
        {
            await unitOfWork.SalvarAlteracoesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception falha) when (IdentificadorLegivelJaDivulgadoException.EhViolacaoDoIndice(falha))
        {
            unitOfWork.DescartarAlteracoesNaoSalvas();

            CertameDivulgado? dono = await certameDivulgadoRepository
                .ObterParaLeituraPorIdentificadorAsync(certame.IdentificadorLegivel, cancellationToken)
                .ConfigureAwait(false);

            // Linha do próprio processo (entregas de versões dele correndo juntas) ou já removida:
            // é corrida, que a reentrega resolve — a falha original segue para a política transiente.
            if (IdentificadorLegivelJaDivulgadoException.Classificar(
                falha, versao.ProcessoSeletivoId, dono?.Id, certame.IdentificadorLegivel) is not { } conflito)
            {
                throw;
            }

            throw conflito;
        }
    }

    /// <summary>
    /// Copia para o acervo público o documento do edital que a versão congelou, na chave do ato que a
    /// criou.
    /// </summary>
    /// <remarks>
    /// Mesma conferência dos modelos: do cadastro só se lê onde está a cópia selada privada, e um
    /// documento sem cópia selada ou com outro conteúdo não é publicado — o endereço divulgado
    /// apontaria para um arquivo que não é o que o ato oficializou.
    /// </remarks>
    private static async Task<DocumentoDivulgado> PublicarEditalNoAcervoAsync(
        VersaoConfiguracao versao,
        AtoDaDivulgacao ato,
        JsonObject envelope,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        if (!ProjecaoDoCertamePublicado.TentarEditalNoAcervo(envelope, ato, out EditalNoAcervo? edital))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada no documento do edital.");
        }

        DocumentoAPublicar? documento = await documentoAPublicarRepository
            .ObterPorIdAsync(edital.DocumentoEditalId, cancellationToken)
            .ConfigureAwait(false);
        if (documento is null
            || documento.ProcessoSeletivoId != versao.ProcessoSeletivoId
            || documento.ObjectKeyConfirmado is not { } chavePrivada
            || !string.Equals(documento.HashSha256, edital.HashSha256, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"O documento do edital {edital.DocumentoEditalId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
        }

        await acervoPublico
            .CopiarAsync(
                chavePrivada,
                edital.Chave,
                new DocumentoNoAcervo(
                    documento.TipoDeMidia,
                    edital.NomeDeApresentacao,
                    versao.AtoCriadorId,
                    versao.ProcessoSeletivoId,
                    edital.HashSha256),
                cancellationToken)
            .ConfigureAwait(false);

        return new DocumentoDivulgado(
            PapelDoDocumentoDivulgado.Principal,
            edital.NomeDeApresentacao,
            documento.TipoDeMidia,
            documento.TamanhoBytes ?? 0,
            edital.HashSha256,
            enderecoNoAcervo.De(edital.Chave).OriginalString);
    }

    /// <summary>
    /// Copia para o acervo público cada anexo que a versão congelou, na chave do ato que a criou, e
    /// devolve os documentos como o acervo passa a servi-los.
    /// </summary>
    /// <remarks>
    /// Mesma conferência do edital e dos modelos: nome, formato, tamanho e hash são os do envelope, e
    /// do cadastro só se lê onde está a cópia selada privada. O anexo sem cópia selada, ou com outro
    /// conteúdo, é dado inconsistente — divulgar o endereço de um arquivo que não é o congelado seria
    /// pior que não divulgar.
    /// </remarks>
    private static async Task<IReadOnlyList<DocumentoDivulgado>> PublicarAnexosNoAcervoAsync(
        VersaoConfiguracao versao,
        JsonObject envelope,
        IDocumentoAPublicarRepository documentoAPublicarRepository,
        IAcervoPublico acervoPublico,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        if (!ProjecaoDoCertamePublicado.TentarAnexosCongelados(envelope, out List<AnexoDoEdital>? anexos))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos anexos do edital.");
        }

        if (anexos.Count == 0)
        {
            return [];
        }

        IReadOnlyList<DocumentoAPublicar> cadastrados = await documentoAPublicarRepository
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. anexos.Select(static a => a.DocumentoId)], cancellationToken)
            .ConfigureAwait(false);

        List<DocumentoDivulgado> divulgados = [];
        foreach (AnexoDoEdital anexo in anexos)
        {
            DocumentoAPublicar? cadastrado = cadastrados.FirstOrDefault(d => d.Id == anexo.DocumentoId);
            if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
                || !string.Equals(cadastrado.HashSha256, anexo.HashSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"O anexo {anexo.Id} do edital, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
            }

            string chave = ChaveNoAcervoPublico.DoAnexo(versao.ProcessoSeletivoId, versao.AtoCriadorId, anexo);
            await acervoPublico
                .CopiarAsync(
                    chavePrivada,
                    chave,
                    new DocumentoNoAcervo(
                        anexo.TipoDeMidia,
                        anexo.NomeArquivo,
                        versao.AtoCriadorId,
                        versao.ProcessoSeletivoId,
                        anexo.HashSha256),
                    cancellationToken)
                .ConfigureAwait(false);

            divulgados.Add(new DocumentoDivulgado(
                PapelDoDocumentoDivulgado.Anexo,
                anexo.NomeArquivo,
                anexo.TipoDeMidia,
                anexo.TamanhoBytes,
                anexo.HashSha256,
                enderecoNoAcervo.De(chave).OriginalString));
        }

        return divulgados;
    }

    /// <summary>
    /// Copia para o acervo público cada modelo de documento que a versão congelou, na chave do ato
    /// que a criou.
    /// </summary>
    /// <remarks>
    /// O que se publica vem do envelope — nome, formato e hash são os do edital —, e do cadastro só se
    /// lê onde está a cópia selada privada. Modelo sem cópia selada, ou com outro conteúdo, é dado
    /// inconsistente: divulgar o link de um arquivo que não é o congelado seria pior que não divulgar.
    /// </remarks>
    private static async Task PublicarModelosNoAcervoAsync(
        VersaoConfiguracao versao,
        JsonObject envelope,
        IModeloDeDocumentoRepository modeloDeDocumentoRepository,
        IAcervoPublico acervoPublico,
        CancellationToken cancellationToken)
    {
        if (!ProjecaoDoCertamePublicado.TentarModelosCongelados(envelope, out List<ModeloDaExigencia>? modelos))
        {
            throw new InvalidOperationException(
                $"Configuração congelada do processo {versao.ProcessoSeletivoId} não tem a forma esperada nos modelos de documento.");
        }

        if (modelos.Count == 0)
        {
            return;
        }

        IReadOnlyList<ModeloDeDocumento> cadastrados = await modeloDeDocumentoRepository
            .ListarDoProcessoAsync(versao.ProcessoSeletivoId, [.. modelos.Select(static m => m.ModeloId)], cancellationToken)
            .ConfigureAwait(false);

        foreach (ModeloDaExigencia modelo in modelos)
        {
            ModeloDeDocumento? cadastrado = cadastrados.FirstOrDefault(m => m.Id == modelo.ModeloId);
            if (cadastrado?.ObjectKeyConfirmado is not { } chavePrivada
                || !string.Equals(cadastrado.HashSha256, modelo.HashSha256, StringComparison.Ordinal))
            {
                throw new InvalidOperationException(
                    $"O modelo de documento {modelo.ModeloId}, congelado na versão {versao.NumeroVersao} do processo {versao.ProcessoSeletivoId}, não tem cópia selada com o conteúdo congelado.");
            }

            await acervoPublico
                .CopiarAsync(
                    chavePrivada,
                    ChaveNoAcervoPublico.DoModelo(versao.ProcessoSeletivoId, versao.AtoCriadorId, modelo),
                    new DocumentoNoAcervo(
                        FormatosDeDocumento.TipoDeMidia(modelo.Formato),
                        modelo.NomeArquivo,
                        versao.AtoCriadorId,
                        versao.ProcessoSeletivoId,
                        modelo.HashSha256),
                    cancellationToken)
                .ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Lê o documento congelado como objeto JSON, sem deixar passar exceção de análise.
    /// </summary>
    /// <remarks>
    /// Texto que não fecha como JSON faz <c>JsonNode.Parse</c> LANÇAR, e a exceção crua escaparia
    /// antes da recusa nomeada logo abaixo — a fila morta receberia um <c>JsonException</c> sem o
    /// identificador do processo, que é a única pista que alguém teria para investigar.
    /// </remarks>
    private static bool TentarLerEnvelope(string congelado, [NotNullWhen(true)] out JsonObject? envelope)
    {
        try
        {
            envelope = JsonNode.Parse(congelado) as JsonObject;
        }
        catch (JsonException)
        {
            envelope = null;
        }

        return envelope is not null;
    }
}
