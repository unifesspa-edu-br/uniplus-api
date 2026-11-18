namespace Unifesspa.UniPlus.Selecao.Application.Services.DocumentoDaConfiguracao;

using System.Text.Json.Nodes;

using Kernel.Results;

/// <summary>
/// Leva o documento enviado à forma que o esquema avalia, guiada pelo próprio esquema (nenhum campo ou parte é citado aqui):
/// retira todo campo marcado como só de leitura, porque o sistema o define e a importação o ignora, e aponta as chaves que
/// se repetem. A chave é de quem cria a parte: a que falta não é atribuída, e o esquema a recusa. Não toca no que o esquema
/// não conhece nem no que está presente, ainda que vazio (<c>""</c>, <c>[]</c>, <c>null</c>): a avaliação recusa o que
/// estiver errado. Altera o documento recebido.
/// </summary>
public sealed class NormalizacaoDoDocumento(FormaDoNo forma)
{
    public IReadOnlyList<FieldError> Normalizar(JsonObject documento)
    {
        ArgumentNullException.ThrowIfNull(documento);

        RetiradaDosCamposSomenteLeitura.Retirar(NosDeclaradosNoEsquema.Listar(documento, forma));

        // O percurso refeito já não passa pelo que foi retirado, e é por ele que as chaves se conferem.
        return ConferenciaDasChaves.Repetidas(NosDeclaradosNoEsquema.Listar(documento, forma));
    }
}
