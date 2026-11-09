namespace Unifesspa.UniPlus.Autorizacao.Domain.Entities;

using Unifesspa.UniPlus.Kernel.Domain.Enums;

/// <summary>
/// Permissão do catálogo reunida por uma versão do perfil. Além do código, guarda a classificação e o alcance que o
/// catálogo lhe dava ao criar a versão; a decisão de acesso não os usa, e o catálogo continua sendo o dono do dado vivo.
/// </summary>
public sealed class PermissaoDaVersao
{
    public string Codigo { get; private set; }

    public ClassificacaoDeProtecao Classificacao { get; private set; }

    public bool DeUnidade { get; private set; }

    internal PermissaoDaVersao(string codigo, ClassificacaoDeProtecao classificacao, bool deUnidade)
    {
        Codigo = codigo;
        Classificacao = classificacao;
        DeUnidade = deUnidade;
    }
}
