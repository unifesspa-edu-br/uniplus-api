namespace Unifesspa.UniPlus.Selecao.IntegrationTests.Errors;

using System.Text.Encodings.Web;
using System.Text.Json;

using AwesomeAssertions;

using Microsoft.AspNetCore.Mvc;

using Unifesspa.UniPlus.Infrastructure.Core.Errors;
using Unifesspa.UniPlus.Kernel.Results;
using Unifesspa.UniPlus.Selecao.Application.Commands.ProcessosSeletivos;
using Unifesspa.UniPlus.Selecao.Domain.Entities;
using Unifesspa.UniPlus.Selecao.Domain.Enums;
using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Recursos;
using Unifesspa.UniPlus.Selecao.Domain.ValueObjects;

/// <summary>
/// O procedimento de recurso ou a versão que o catálogo não conhece é recusado sem devolver o que a pessoa digitou: o texto
/// enviado, que pode ter dado pessoal por engano, não reaparece no detalhe nem na mensagem do campo.
/// </summary>
public sealed class RecusaDoProcedimentoDeRecursoDesconhecidoNaRespostaTests
{
    private const string CodigoDigitado = "MARCADOR-FICTICIO-DO-CODIGO";
    private const string VersaoDigitada = "MARCADOR-FICTICIA-DA-VERSAO";

    private static readonly JsonSerializerOptions Texto = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    private static readonly IDomainErrorMapper Mapper = MapperDoModuloDeTeste.Criar();

    [Fact(DisplayName = "O corpo da resposta da recusa não traz o código nem a versão enviados e aponta o campo da regra")]
    public async Task Resposta_NaoRepeteOCodigoNemAVersaoEnviados()
    {
        DeclaracaoDosProcedimentosDeRecurso declaracao = new(
            static (_, _, _) => Task.FromResult<RegraCatalogo?>(null), CodigosDoProcedimentoDeRecurso.DaEtapa);
        ProcedimentoDeRecursoInput entrada = new(TipoMotivoDeDecisaoCodigo.Classificacao, null, CodigoDigitado, VersaoDigitada);
        ContextoDosProcedimentosDeRecurso contexto = new(new HashSet<TipoMotivoDeDecisao> { TipoMotivoDeDecisao.Classificacao }, new HashSet<Guid>(), false);

        Result<ProcedimentosDeRecurso> recusa = await declaracao.DeclararAsync([entrada], contexto, "recursos[0].procedimentos", CancellationToken.None);
        ObjectResult resposta = (ObjectResult)recusa.ToActionResult(Mapper);
        string corpo = JsonSerializer.Serialize(resposta.Value, Texto);

        corpo.Should().Contain("recursos[0].procedimentos[0].regra").And.Contain("não consta do catálogo");
        corpo.Should().NotContain(CodigoDigitado).And.NotContain(VersaoDigitada);
    }
}
