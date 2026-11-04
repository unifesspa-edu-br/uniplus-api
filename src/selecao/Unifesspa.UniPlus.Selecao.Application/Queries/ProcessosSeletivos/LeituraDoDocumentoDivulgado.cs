namespace Unifesspa.UniPlus.Selecao.Application.Queries.ProcessosSeletivos;

using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;

using Domain.Entities;
using Domain.Enums;
using Domain.Services;
using Domain.ValueObjects;

using DTOs;

using Unifesspa.UniPlus.Kernel.Results;

/// <summary>
/// Leitura do documento já divulgado, guardado como a resposta pública do certame: confere se ele é uma projeção íntegra da versão corrente antes de servi-lo.
/// </summary>
internal static class LeituraDoDocumentoDivulgado
{
    /// <summary>
    /// Lê a projeção já materializada na linha de divulgação. Devolve <see langword="false"/>
    /// quando o documento guardado não é uma projeção íntegra.
    /// </summary>
    /// <remarks>
    /// Uma só implementação para os dois contratos públicos do certame — o detalhe recusa com
    /// <see cref="ProjecaoDoCertamePublicado.CodigoEnvelopeInesperado"/>, a vitrine omite o item, e ambos partem da mesma
    /// leitura. Texto que não fecha como JSON faz <c>JsonSerializer.Deserialize</c> LANÇAR (não
    /// devolver <see langword="null"/>, que é reservado ao literal <c>null</c>), e um documento que
    /// desserializa sem os blocos obrigatórios devolve membros nulos que estouram no primeiro
    /// acesso. Os dois casos só são alcançáveis por linha adulterada no banco ou por projeção de
    /// formato anterior, e os dois viram recusa em vez de 500 num endereço anônimo.
    /// </remarks>
    internal static bool TentarLerProjecao(
        string documentoDivulgado,
        [NotNullWhen(true)] out CertamePublicadoDto? certame)
    {
        certame = null;
        CertamePublicadoDto? lido;

        try
        {
            lido = JsonSerializer.Deserialize<CertamePublicadoDto>(documentoDivulgado, ProjecaoDoCertamePublicado.OpcoesDoDocumento);
        }
        catch (JsonException)
        {
            return false;
        }

        if (!EhProjecaoIntegra(lido))
        {
            return false;
        }

        certame = lido;
        return true;
    }

    /// <summary>
    /// O documento desserializou; resta saber se o que saiu dele é uma projeção inteira desta
    /// versão.
    /// </summary>
    private static bool EhProjecaoIntegra([NotNullWhen(true)] CertamePublicadoDto? certame)
    {
        // Um documento de formato anterior desserializa sem lançar e deixa os blocos que ele não
        // tinha como nulos. Conferir aqui é o que impede o NullReferenceException lá adiante, em
        // quem monta a vitrine ou o corpo da resposta — e o que impede servir campo declarado
        // obrigatório com valor nulo, que é a meia projeção que esta classe recusa. A conferência
        // cobre TODOS os membros não-anuláveis do contrato, porque um subconjunto deles deixa o
        // resto passar em silêncio. O ELEMENTO nulo dentro de um arranjo é recusado antes, na
        // desserialização, por ArranjoSemElementoNulo.
        return certame is not null
            && certame.IdentificadorLegivel is not null
            && certame.Nome is not null
            // Igualdade, não presença: num deploy em fases um processo novo materializa a versão
            // seguinte e este continua lendo a mesma linha. O documento desserializa — os campos
            // que ele não conhece são ignorados — e a resposta sairia com a forma antiga carimbada,
            // no corpo e no ETag, com a identidade da versão nova: um cache guardaria conteúdo
            // incompleto sob o selo do completo.
            && certame.VersaoProjecao == ProjecaoDoCertamePublicado.Versao
            && certame.HashConfiguracao is not null
            && certame.TipoProcesso is not null
            && certame.Periodo is not null
            && certame.Localidade is not null
            && certame.UnidadeAdministradora is not null
            && certame.DocumentoEdital is not null
            && certame.OrigemCandidatos is not null
            && certame.ModalidadesOfertadas is not null
            && certame.Ofertas is not null
            && certame.OpcoesDeCurso is not null
            && certame.Vagas is not null
            && certame.Etapas is not null
            && certame.CronogramaFases is not null
            && certame.DocumentosExigidos is not null
            && certame.Atendimento is not null;
    }

    /// <summary>
    /// Recusa a leitura de um documento divulgado que não é uma projeção íntegra — a mesma recusa
    /// que um bloco de forma inesperada produz, pela mesma razão: é falha de leitura, e uma exceção
    /// solta aqui viraria 500 num endereço anônimo.
    /// </summary>
    internal static Result<CertamePublicadoDto> RecusarDocumentoIlegivel() => ProjecaoDoCertamePublicado.Recusar<CertamePublicadoDto>("documento do certame");
}
