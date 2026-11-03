namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Regras.Errors;
using Unifesspa.UniPlus.Regras.Formularios;
using Unifesspa.UniPlus.Regras.Services;
using Unifesspa.UniPlus.Regras.ValueObjects;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ProtecaoDasExigencias
{
    internal static void Recongelar(IEnumerable<DocumentoExigido> exigencias, ProtecaoVigenteDasExigencias protecao)
    {
        ArgumentNullException.ThrowIfNull(protecao);

        foreach (DocumentoExigido exigencia in exigencias)
        {
            if (protecao.PorExigencia.TryGetValue(exigencia.Id, out ProtecaoDoDocumento? efetiva))
            {
                exigencia.RecongelarProtecao(efetiva);
            }
        }
    }

    /// <summary>
    /// A finalidade de cada exigência continua coerente com a fase dela: a escrita confere, e o
    /// cronograma pode ter tirado da fase, depois, o formulário que a exigência declara.
    /// </summary>
    internal static DomainError? PendenciaDaFinalidadeDasExigencias(EstadoDaConfiguracao estado) =>
        estado.DocumentosExigidos
            .Select(exigencia => estado.CronogramaFases.FirstOrDefault(f => f.Id == exigencia.ExigidoNaFaseId) is { } fase
                ? Services.ValidadorVinculoDaExigencia.RecusaDaFinalidade(exigencia.Finalidade, fase)
                : null)
            .FirstOrDefault(static recusa => recusa is not null);

    internal static ClassificacaoDeProtecao ClassificacaoEfetiva(DocumentoExigido exigencia, ProtecaoVigenteDasExigencias? protecao) =>
        protecao is not null && protecao.PorExigencia.TryGetValue(exigencia.Id, out ProtecaoDoDocumento? efetiva)
            ? efetiva.Classificacao
            : exigencia.ClassificacaoProtecao;

    /// <summary>
    /// Cada fato que uma exigência diz comprovar é coletado por algum formulário do processo ou é fato
    /// derivado congelado na publicação: só assim tem classificação congelada com que comparar.
    /// </summary>
    internal static DomainError? PendenciaDoFatoComprovadoForaDoProcesso(EstadoDaConfiguracao estado, ProtecaoVigenteDasExigencias? protecao)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos)
        {
            foreach (string codigo in exigencia.FatosComprovados.Order(StringComparer.Ordinal))
            {
                if (protecao is not null
                    && !protecao.ClassificacaoDosFatosDerivados.ContainsKey(codigo)
                    && !protecao.DerivadosSemProtecaoAprovada.Contains(codigo)
                    && !estado.Campos.Any(f => string.Equals(f.FatoCodigo, codigo, StringComparison.Ordinal)))
                {
                    return new DomainError(
                        DocumentoExigido.FatoComprovadoForaDoProcesso,
                        $"A exigência do tipo de documento '{exigencia.TipoDocumentoCodigo}' comprova o fato '{codigo}', que o processo não coleta e que não é fato derivado do catálogo.");
                }
            }
        }

        return null;
    }

    /// <summary>
    /// O tipo de documento congelado na exigência é tão protegido quanto o mais protegido dos fatos
    /// que ela comprova, na classificação que cada fato tem congelada no processo: um laudo
    /// classificado como pessoal não comprova um fato sensível, porque o arquivo do laudo carrega
    /// o dado do fato. O fato que só aciona a exigência pelo gatilho não entra na conferência, e a
    /// exigência sem fato comprovado vale pela classificação do tipo. O fato que o processo não
    /// coleta é outra pendência (<see cref="PendenciaDoFatoComprovadoForaDoProcesso"/>) e não conta aqui.
    /// </summary>
    internal static DomainError? PendenciaDaProtecaoDasExigencias(EstadoDaConfiguracao estado, ProtecaoVigenteDasExigencias? protecao)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos.Where(static e => e.FatosComprovados.Length > 0))
        {
            (string Codigo, ClassificacaoDeProtecao Nivel)? maisProtegido = null;
            foreach (string codigo in exigencia.FatosComprovados.Order(StringComparer.Ordinal))
            {
                ClassificacaoDeProtecao[] daColeta =
                [
                    .. estado.Campos.Where(f => string.Equals(f.FatoCodigo, codigo, StringComparison.Ordinal))
                        .Select(static f => ClassificacoesDeProtecao.Analisar(f.ClassificacaoProtecao)),
                ];
                ClassificacaoDeProtecao nivel;
                if (daColeta.Length > 0)
                {
                    nivel = daColeta.Max();
                }
                else if (protecao?.ClassificacaoDosFatosDerivados.TryGetValue(codigo, out ClassificacaoDeProtecao doDerivado) == true)
                {
                    nivel = doDerivado;
                }
                else
                {
                    continue;
                }

                if (maisProtegido is not { } atual || nivel > atual.Nivel)
                {
                    maisProtegido = (codigo, nivel);
                }
            }

            ClassificacaoDeProtecao doTipo = ClassificacaoEfetiva(exigencia, protecao);
            if (maisProtegido is { } decisivo && doTipo < decisivo.Nivel)
            {
                return new DomainError(
                    ProcessoSeletivoErrorCodes.ExigenciaMenosProtegidaQueFatoComprovado,
                    $"A exigência do tipo de documento '{exigencia.TipoDocumentoCodigo}' ({exigencia.TipoDocumentoNome}) é {ClassificacoesDeProtecao.ParaTokenCanonico(doTipo)} "
                    + $"e comprova o fato '{decisivo.Codigo}', que é {ClassificacoesDeProtecao.ParaTokenCanonico(decisivo.Nivel)}. "
                    + "Exija um tipo de documento com ao menos essa proteção ou retire o fato da exigência.");
            }
        }

        return null;
    }

    /// <summary>
    /// Pendências dos documentos exigidos (Story #554). Chamado por
    /// <see cref="Publicar"/> e por <see cref="SucederVersao"/> (Retificar/
    /// FecharRetificacao), sempre <b>depois</b> de <see cref="PendenciaDoCronograma"/>.
    /// </summary>
    /// <remarks>
    /// <b>CA-01</b> (Story #554, issue #547) — uma exigência <c>CONDICIONAL</c> sem
    /// nenhuma condição de gatilho viva que <see cref="DocumentoExigido.DeterminaResultado"/>
    /// é "exigência morta": nunca seria cobrada de ninguém. Desde a PR #896 (issue #892),
    /// avalia a coleção REAL de <see cref="DocumentoExigido.Condicoes"/>.
    /// <para>
    /// A guarda fail-closed <b>B-01</b>, que bloqueava QUALQUER publicação com
    /// <see cref="DocumentoExigido"/> configurado enquanto o bloco
    /// <c>documentosExigidos.exigencias</c> do envelope era stub (PR #895..PR #900), foi
    /// <b>removida na PR #903</b> (issue #548): o bloco rico a substitui, e o gate real
    /// (<see cref="Services.AvaliadorConformidadeLegal.Avaliar"/>, predicado
    /// <c>DocumentoObrigatorioParaModalidade</c>) passa a decidir com base no que está
    /// realmente configurado, não mais reprovando por padrão conservador.
    /// </para>
    /// </remarks>
    internal static DomainError? PendenciaDasExigenciasDocumentais(EstadoDaConfiguracao estado)
    {
        foreach (DocumentoExigido exigencia in estado.DocumentosExigidos)
        {
            bool possuiCondicaoViva = exigencia.Condicoes.Count > 0;
            if (exigencia.Aplicabilidade == Aplicabilidade.Condicional
                && !possuiCondicaoViva
                && exigencia.DeterminaResultado())
            {
                return new DomainError(
                    "DocumentoExigido.CondicionalVaziaDeterminaResultado",
                    $"A exigência '{exigencia.TipoDocumentoCodigo}' é CONDICIONAL, determina resultado, mas não tem nenhuma condição de gatilho viva — nunca seria cobrada de ninguém.");
            }
        }

        return null;
    }
}
