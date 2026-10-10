namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using Abstractions;

using Domain.Entities;
using Domain.Interfaces;
using Domain.ValueObjects;

using DTOs;

using Services;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Handler do <see cref="ObterFormularioRenderizavelQuery"/> (RN08, UNI-REQ-0072): projeta o
/// formulário renderizável da finalidade a partir da versão que o certame <b>divulgado</b> serve —
/// a apresentação e as regras saem do grafo reidratado, pela mesma projeção do rascunho
/// (<see cref="ProjecaoDoFormularioRenderizavel"/>).
/// </summary>
/// <remarks>
/// <para>
/// <b>Resolve pela divulgação, e não pelo relógio.</b> A leitura pública do certame é uma consulta à
/// tabela de divulgações, cuja existência de linha É a publicidade. Enquanto o ato de uma
/// retificação não se confirma, a divulgação não avança: a página do certame serve a versão
/// anterior, que é a que tem publicidade. Resolver o formulário pela versão vigente por relógio o
/// faria servir a versão NOVA — o candidato leria um edital e preencheria o formulário de outro, e
/// os dados seriam coletados sob uma configuração que ainda não tem ato normativo e que pode nunca
/// vir a ter, se o registro for recusado por mérito.
/// </para>
/// <para>
/// <b>Uma recusa só.</b> Processo inexistente, em rascunho, sem versão vigente e sem divulgação
/// caem no mesmo não encontrado — não por uma regra que colapse os casos, mas porque nenhum deles
/// tem linha. Distinguir rascunho de inexistente numa rota anônima é responder a um estranho se um
/// identificador corresponde a um processo que ainda não é público.
/// </para>
/// <para>
/// Antes de projetar, confere a <c>SchemaVersion</c> contra as capacidades de leitura que
/// <see cref="IRegistroCodecsEnvelope"/> declara: sob o regime de codec único reescrito no lugar
/// (ADR-0110 Emenda 2, ADR-0109 Emenda 2), uma versão que deixou de ser a corrente não ganha
/// decodificador próprio, e bytes que coincidentemente têm a forma atual não a tornam reconhecida.
/// </para>
/// </remarks>
public static class ObterFormularioRenderizavelQueryHandler
{
    public static async Task<Result<FormularioRenderizavelDto>> Handle(
        ObterFormularioRenderizavelQuery query,
        IProcessoSeletivoRepository processoSeletivoRepository,
        ICertameDivulgadoRepository certameDivulgadoRepository,
        IRegistroCodecsEnvelope registroCodecs,
        IEnderecoNoAcervoPublico enderecoNoAcervo,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentNullException.ThrowIfNull(processoSeletivoRepository);
        ArgumentNullException.ThrowIfNull(certameDivulgadoRepository);
        ArgumentNullException.ThrowIfNull(registroCodecs);
        ArgumentNullException.ThrowIfNull(enderecoNoAcervo);

        CertameDivulgado? divulgado = await certameDivulgadoRepository
            .ObterParaLeituraAsync(query.ProcessoSeletivoId, cancellationToken)
            .ConfigureAwait(false);

        if (divulgado is null)
        {
            return NaoEncontrado(query.ProcessoSeletivoId);
        }

        // A linha aponta o ato que confirmou a publicidade, e é por ele que se chega à versão
        // exata que o certame serve — não à mais nova, que sob retificação pendente ainda não tem
        // publicidade nenhuma.
        IReadOnlyList<VersaoConfiguracao> versoes = await processoSeletivoRepository
            .ObterVersoesPorAtoCriadorAsync([divulgado.AtoCriadorId], cancellationToken)
            .ConfigureAwait(false);

        if (versoes.Count == 0)
        {
            // Divulgação sem a versão que ela aponta é corrupção, não ausência: a linha só nasce
            // junto da versão. Recusar como não encontrado esconderia o defeito atrás de uma
            // resposta plausível — e aqui não há oráculo a proteger, porque a existência da linha
            // já disse que o processo é público.
            return Result<FormularioRenderizavelDto>.Failure(new DomainError(
                "Snapshot.VigenteAusente",
                $"A divulgação do processo {query.ProcessoSeletivoId} aponta uma versão de configuração que não existe."));
        }

        VersaoConfiguracao versao = versoes[0];

        Result<FormularioRenderizavelDto> projetado = ProjecaoDoFormularioDaVersao.Projetar(
            versao, query.Finalidade, divulgado.AtoCriadorId, registroCodecs, enderecoNoAcervo);

        // A projeção compartilhada nomeia a ausência de formulário da finalidade; a leitura
        // pública a colapsa no mesmo não encontrado dos demais casos sem linha (ver remarks).
        return projetado.IsFailure && projetado.Error!.Code == "FormularioInscricao.FinalidadeSemFormulario"
            ? NaoEncontrado(query.ProcessoSeletivoId)
            : projetado;
    }

    /// <summary>
    /// A recusa única da leitura pública: inexistente, rascunho, sem versão vigente e sem
    /// divulgação recebem a mesma resposta, porque nenhum deles tem linha.
    /// </summary>
    private static Result<FormularioRenderizavelDto> NaoEncontrado(Guid processoSeletivoId) =>
        Result<FormularioRenderizavelDto>.Failure(new DomainError(
            "ProcessoSeletivo.NaoEncontrado",
            $"Processo Seletivo {processoSeletivoId} não encontrado."));
}
