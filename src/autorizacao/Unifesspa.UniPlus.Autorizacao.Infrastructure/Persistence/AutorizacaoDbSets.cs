namespace Unifesspa.UniPlus.Autorizacao.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Unifesspa.UniPlus.Autorizacao.Domain.Entities;
using Unifesspa.UniPlus.Infrastructure.Core.Persistence;

/// <summary>As coleções das entidades do módulo, expostas no contexto único para consultas, repositórios e testes.</summary>
public static class AutorizacaoDbSets
{
    extension(UniPlusDbContext contexto)
    {
        public DbSet<Perfil> Perfis => contexto.Set<Perfil>();

        public DbSet<VersaoDoPerfil> VersoesDosPerfis => contexto.Set<VersaoDoPerfil>();

        public DbSet<VigenciaDoPerfil> VigenciasDosPerfis => contexto.Set<VigenciaDoPerfil>();

        public DbSet<DecisaoDaVersao> DecisoesDasVersoes => contexto.Set<DecisaoDaVersao>();

        public DbSet<RecalculoDaAprovacao> RecalculosDasAprovacoes => contexto.Set<RecalculoDaAprovacao>();

        public DbSet<DocumentoDaVersao> DocumentosDasVersoes => contexto.Set<DocumentoDaVersao>();

        public DbSet<LeituraDoDocumentoDaVersao> LeiturasDosDocumentosDasVersoes => contexto.Set<LeituraDoDocumentoDaVersao>();

        public DbSet<SuspensaoDoPerfil> SuspensoesDosPerfis => contexto.Set<SuspensaoDoPerfil>();

        public DbSet<DecisaoDaRetomada> DecisoesDasRetomadas => contexto.Set<DecisaoDaRetomada>();

        public DbSet<DesativacaoDoPerfil> DesativacoesDosPerfis => contexto.Set<DesativacaoDoPerfil>();

        public DbSet<EventoDaContencao> EventosDaContencao => contexto.Set<EventoDaContencao>();

        public DbSet<Concessao> Concessoes => contexto.Set<Concessao>();

        public DbSet<JanelaDeMigracao> JanelasDeMigracao => contexto.Set<JanelaDeMigracao>();

        public DbSet<LoteDaMigracao> LotesDaMigracao => contexto.Set<LoteDaMigracao>();

        public DbSet<MigracaoDaConcessao> MigracoesDasConcessoes => contexto.Set<MigracaoDaConcessao>();

        public DbSet<DecisaoDaMigracao> DecisoesDaMigracao => contexto.Set<DecisaoDaMigracao>();

        public DbSet<Instalacao> Instalacoes => contexto.Set<Instalacao>();

        public DbSet<TentativaDeInstalacao> TentativasDeInstalacao => contexto.Set<TentativaDeInstalacao>();

        public DbSet<PedidoDeConcessao> Pedidos => contexto.Set<PedidoDeConcessao>();

        public DbSet<DecisaoDoPedido> DecisoesDosPedidos => contexto.Set<DecisaoDoPedido>();

        public DbSet<TrocaDeVersaoDoPedido> TrocasDeVersaoDosPedidos => contexto.Set<TrocaDeVersaoDoPedido>();

        public DbSet<AnexoDoPedido> AnexosDosPedidos => contexto.Set<AnexoDoPedido>();

        public DbSet<LeituraDoAnexoDoPedido> LeiturasDosAnexos => contexto.Set<LeituraDoAnexoDoPedido>();

        public DbSet<TermoDeResponsabilidade> TermosDeResponsabilidade => contexto.Set<TermoDeResponsabilidade>();

        public DbSet<VersaoDoTermo> VersoesDoTermo => contexto.Set<VersaoDoTermo>();

        public DbSet<AceiteDoTermo> AceitesDoTermo => contexto.Set<AceiteDoTermo>();

        public DbSet<TermoNaInstalacao> TermosNaInstalacao => contexto.Set<TermoNaInstalacao>();

        public DbSet<RevisaoDeAcessos> Revisoes => contexto.Set<RevisaoDeAcessos>();

        public DbSet<ItemDaRevisao> ItensDasRevisoes => contexto.Set<ItemDaRevisao>();

        public DbSet<Providencia> Providencias => contexto.Set<Providencia>();

        public DbSet<ImplantacaoDasPermissoes> ImplantacoesDasPermissoes => contexto.Set<ImplantacaoDasPermissoes>();

        public DbSet<PermissaoImplantada> PermissoesImplantadas => contexto.Set<PermissaoImplantada>();

        public DbSet<BaseDaPermissaoNaConcessao> BasesDasPermissoes => contexto.Set<BaseDaPermissaoNaConcessao>();

        public DbSet<ReclassificacaoDaPermissao> Reclassificacoes => contexto.Set<ReclassificacaoDaPermissao>();

        public DbSet<LiberacaoDaReclassificacao> LiberacoesDasReclassificacoes => contexto.Set<LiberacaoDaReclassificacao>();

        public DbSet<DivergenciaDaGovernanca> DivergenciasDaGovernanca => contexto.Set<DivergenciaDaGovernanca>();

        public DbSet<AvisoDaReclassificacao> AvisosDasReclassificacoes => contexto.Set<AvisoDaReclassificacao>();
    }
}
