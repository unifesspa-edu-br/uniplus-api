namespace Unifesspa.UniPlus.Autorizacao.Application.UnitTests;

using AwesomeAssertions;

using Unifesspa.UniPlus.Authorization.Catalogo;
using Unifesspa.UniPlus.Authorization.Enums;
using Unifesspa.UniPlus.Autorizacao.Application.DTOs;
using Unifesspa.UniPlus.Autorizacao.Application.Mappings;
using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Autorizacao.Domain.Interfaces;

public sealed class PerfilMappingTests
{
    private static readonly CatalogoDaAplicacao Catalogo = new([new Fonte()]);

    [Fact]
    public void Sensibilidade_do_perfil_e_a_mais_alta_entre_as_permissoes()
    {
        PerfilDto dto = PerfilMapping.ToDto(
            CriarPerfil("teste:itens:ler", "teste:itens:manter", "teste:laudos:consultar"),
            Catalogo);

        dto.Sensibilidade.Should().Be(ValoresCanonicos.De(Sensibilidade.Sensivel));
    }

    [Fact]
    public void Permissao_retirada_do_catalogo_conta_como_sensivel()
    {
        Perfil perfil = CriarPerfil("teste:itens:ler", "teste:itens:retirada");

        PerfilDto dto = PerfilMapping.ToDto(perfil, new CatalogoDaAplicacao([]));

        dto.Sensibilidade.Should().Be(ValoresCanonicos.De(Sensibilidade.Sensivel));
    }

    private static Perfil CriarPerfil(params string[] codigos) =>
        Perfil.Criar("Operação de inscrições", "Opera as inscrições do processo.", codigos, new TodosExistem()).Value!;

    private sealed class TodosExistem : ICatalogoDePermissoesConsultavel
    {
        public bool Existe(string codigo) => true;

        public bool ReservadaAGovernanca(string codigo) => false;
    }

    private sealed class Fonte : IFonteDeCatalogo
    {
        public Type TipoDoEnum => typeof(PermissaoDeTeste);

        public IReadOnlyList<DefinicaoDePermissao> Definicoes() => CatalogoDePermissoes<PermissaoDeTeste>.Todas();
    }

    [ModuloDePermissoes("teste")]
    private enum PermissaoDeTeste
    {
        [Permissao("teste:itens:ler", "Consultar itens", "Lista itens.", Sensibilidade.Interna)]
        Ler = 1,

        [Permissao("teste:itens:manter", "Manter itens", "Cria e edita itens.", Sensibilidade.Pessoal,
            BaseLegal = "LGPD art. 7º, III")]
        Manter = 2,

        [Permissao("teste:laudos:consultar", "Consultar laudos", "Lista laudos médicos.", Sensibilidade.Sensivel,
            BaseLegal = "LGPD art. 11, II, a")]
        ConsultarLaudos = 3,
    }
}
