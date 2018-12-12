using System;
using Microsoft.VisualStudio.Services.Agent.Worker.Container;
using Xunit;

namespace Microsoft.VisualStudio.Services.Agent.Tests.Worker.Container
{
    public sealed class ContainerInfoL0
    {
        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void PortMappingConstructorParsesRawInput()
        {
            // Arrange
            PortMapping containerPort = new PortMapping("80", isExpanded: true);
            PortMapping hostPort_containerPort = new PortMapping("8080:80", isExpanded: true);
            PortMapping containerPort_proto = new PortMapping("80/tcp", isExpanded: true);
            PortMapping hostPort_containerPort_proto = new PortMapping("8080:80/tcp", isExpanded: true);

            // Assert
            Assert.Null(containerPort.HostPort);
            Assert.Null(containerPort.Protocol);
            Assert.Null(containerPort.Raw);
            Assert.True(containerPort.IsExpanded);
            Assert.Equal("80", containerPort.ContainerPort);

            Assert.Null(hostPort_containerPort.Protocol);
            Assert.Null(hostPort_containerPort.Raw);
            Assert.True(hostPort_containerPort.IsExpanded);
            Assert.Equal("8080", hostPort_containerPort.HostPort);
            Assert.Equal("80", hostPort_containerPort.ContainerPort);

            Assert.Null(containerPort_proto.HostPort);
            Assert.Null(containerPort_proto.Raw);
            Assert.True(containerPort_proto.IsExpanded);
            Assert.Equal("80", containerPort_proto.ContainerPort);
            Assert.Equal("tcp", containerPort_proto.Protocol);

            Assert.Null(hostPort_containerPort_proto.Raw);
            Assert.True(hostPort_containerPort_proto.IsExpanded);
            Assert.Equal("8080", hostPort_containerPort_proto.HostPort);
            Assert.Equal("80", hostPort_containerPort_proto.ContainerPort);
            Assert.Equal("tcp", hostPort_containerPort_proto.Protocol);
        }

        [Fact]
        [Trait("Level", "L0")]
        [Trait("Category", "Worker")]
        public void MountVolumeConstructorParsesRawInput()
        {
            // Arrange
            MountVolume target = new MountVolume("/dst/dir", isExpanded: true); // Maps anonymous Docker volume into target dir
            MountVolume source_target = new MountVolume("/src/dir:/dst/dir", isExpanded: true); // Maps source to target dir
            MountVolume target_ro = new MountVolume("/dst/dir:ro", isExpanded: true);
            MountVolume source_target_ro = new MountVolume("/src/dir:/dst/dir:ro", isExpanded: true);

            // Assert
            Assert.Null(target.SourceVolumePath);
            Assert.Equal("/dst/dir", target.TargetVolumePath);
            Assert.True(target.IsExpanded);
            Assert.False(target.ReadOnly);
            Assert.Null(target.Raw);

            Assert.Equal("/src/dir", source_target.SourceVolumePath);
            Assert.Equal("/dst/dir", source_target.TargetVolumePath);
            Assert.True(source_target.IsExpanded);
            Assert.False(source_target.ReadOnly);
            Assert.Null(source_target.Raw);

            Assert.Null(target_ro.SourceVolumePath);
            Assert.Equal("/dst/dir", target_ro.TargetVolumePath);
            Assert.True(target_ro.IsExpanded);
            Assert.True(target_ro.ReadOnly);
            Assert.Null(target_ro.Raw);

            Assert.Equal("/src/dir", source_target_ro.SourceVolumePath);
            Assert.Equal("/dst/dir", source_target_ro.TargetVolumePath);
            Assert.True(source_target_ro.IsExpanded);
            Assert.True(source_target_ro.ReadOnly);
            Assert.Null(source_target_ro.Raw);
        }
    }
}
