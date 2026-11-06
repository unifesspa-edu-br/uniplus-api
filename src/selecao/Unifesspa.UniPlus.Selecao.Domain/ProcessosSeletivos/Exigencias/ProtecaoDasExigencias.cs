namespace Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Exigencias;

using Unifesspa.UniPlus.Kernel.Domain.Enums;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Errors;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Edicao;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

internal static class ProtecaoDasExigencias
{
    internal static PlanoDaProtecaoVigente Planejar(EstadoDaConfiguracao estado, ProtecaoVigenteDasExigencias protecao)
    {
        ArgumentNullException.ThrowIfNull(estado);
        ArgumentNullException.ThrowIfNull(protecao);

        return new PlanoDaProtecaoVigente([.. estado.DocumentosExigidos
            .Where(exigencia => protecao.PorExigencia.ContainsKey(exigencia.Id))
            .Select(exigencia => (exigencia, protecao.PorExigencia[exigencia.Id]))]);
    }

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
}
