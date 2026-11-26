namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.LeituraDoDocumento;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// A leitura da identificação e do cronograma de um documento que passou pela forma: traduz o JSON para as declarações do domínio,
/// junta os códigos que ele cita, lê os cadastros desses códigos uma vez e num instante único, e entrega as declarações lidas pelo
/// domínio. A recusa de uma parte do documento não é falha daqui: viaja na origem do resultado, e o Geo fora do ar vira causa na
/// localidade, sem falhar a leitura. Só o que impede de ler os cadastros (o fuso institucional que o runtime não reconhece) é falha.
/// </summary>
public sealed class LeituraDaIdentificacaoEDoCronograma(LeituraDosCadastrosCitados cadastros)
{
    public async Task<Result<IdentificacaoECronogramaLidos>> LerAsync(JsonObject documento, CancellationToken cancellationToken)
    {
        DeclaracoesDoDocumento declaracoes = DeclaracoesDoDocumentoLidas.De(documento);
        Result<CadastrosCitadosNoDocumento> citados = await cadastros
            .LerAsync(CodigosCitadosNoDocumento.Em(declaracoes), cancellationToken).ConfigureAwait(false);

        return citados.IsFailure
            ? Result<IdentificacaoECronogramaLidos>.Failure(citados.Error!)
            : Result<IdentificacaoECronogramaLidos>.Success(IdentificacaoECronogramaLidos.Ler(declaracoes, citados.Value!));
    }
}
