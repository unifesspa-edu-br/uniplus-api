namespace Unifesspa.UniPlus.Selecao.Domain.UnitTests.ProcessosSeletivos.Assinantes;

using Unifesspa.UniPlus.Selecao.Domain.ProcessosSeletivos.Assinantes;

/// <summary>
/// A estrutura fictícia dos testes: a Diretoria da CGA acima de duas coordenadorias, uma delas com
/// uma seção e uma subseção abaixo; o CEPS fica à parte, sem relação com a CGA.
/// </summary>
internal static class UnidadesDosAssinantesParaTestes
{
    internal static readonly Guid Cga = new("0197d6f0-0000-7000-8000-0000000000a1");
    internal static readonly Guid CoordenadoriaDeEnsino = new("0197d6f0-0000-7000-8000-0000000000a2");
    internal static readonly Guid CoordenadoriaDeRegistro = new("0197d6f0-0000-7000-8000-0000000000a3");
    internal static readonly Guid SecaoDeEditais = new("0197d6f0-0000-7000-8000-0000000000a4");
    internal static readonly Guid SubsecaoDeProvas = new("0197d6f0-0000-7000-8000-0000000000a5");
    internal static readonly Guid Ceps = new("0197d6f0-0000-7000-8000-0000000000b1");

    internal static UnidadeAtiva[] Todas() =>
    [
        new(Cga, null),
        new(CoordenadoriaDeEnsino, Cga),
        new(CoordenadoriaDeRegistro, Cga),
        new(SecaoDeEditais, CoordenadoriaDeEnsino),
        new(SubsecaoDeProvas, SecaoDeEditais),
        new(Ceps, null),
    ];

    internal static EstruturaDasUnidadesAtivas Estrutura() => new(Todas());

    internal static EstruturaDasUnidadesAtivas EstruturaSem(Guid unidadeInativa) =>
        new(Todas().Where(u => u.Id != unidadeInativa));

    internal static Dictionary<string, PessoaDoCadastroDeAssinantes> Cadastro(params PessoaDoCadastroDeAssinantes[] pessoas) =>
        pessoas.ToDictionary(static p => p.Codigo, StringComparer.Ordinal);

    internal static PessoaDoCadastroDeAssinantes Pessoa(
        string codigo, string nome, Guid unidade, bool ativa = true, bool aceiteVigente = true) =>
        new(codigo, nome, unidade, ativa, aceiteVigente);
}
